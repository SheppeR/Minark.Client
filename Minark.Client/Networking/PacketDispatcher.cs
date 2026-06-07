using Minark.Shared.Packets;

namespace Minark.Client.Networking;

/// <summary>
///     Désérialise les bytes entrants et route vers les handlers enregistrés par PacketType.
///     Thread-safe : les handlers sont invoqués depuis le thread réseau WatsonTcp.
/// </summary>
public class PacketDispatcher(ILogger<PacketDispatcher> logger)
{
    private readonly Dictionary<PacketType, List<Action<string>>> _handlers = new();
    private readonly Lock _lock = new();

    /// <summary>
    ///     Envoie un paquet et attend la réponse du type spécifié.
    ///     Enregistre un handler one-shot, envoie, attend (avec timeout), puis nettoie.
    /// </summary>
    public async Task<T> RequestAsync<T>(
        TcpClientService tcp,
        PacketType sendType,
        object sendPayload,
        PacketType recvType,
        T fallback,
        TimeSpan? timeout = null) where T : class
    {
        var tcs = new TaskCompletionSource<T>();

        void Handler(string p)
        {
            tcs.TrySetResult(PacketSerializer.DeserializePayload<T>(p) ?? fallback);
        }

        Register(recvType, Handler);
        await tcp.SendAsync(sendType, sendPayload);
        try
        {
            return await tcs.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(10));
        }
        catch
        {
            return fallback;
        }
        finally
        {
            Unregister(recvType, Handler);
        }
    }

    /// <summary>Variante pour les types valeur ou non-nullable.</summary>
    public async Task<T> RequestStructAsync<T>(
        TcpClientService tcp,
        PacketType sendType,
        object sendPayload,
        PacketType recvType,
        Func<T> fallback,
        TimeSpan? timeout = null) where T : new()
    {
        var tcs = new TaskCompletionSource<T>();

        void Handler(string p)
        {
            tcs.TrySetResult(PacketSerializer.DeserializePayload<T>(p) ?? new T());
        }

        Register(recvType, Handler);
        await tcp.SendAsync(sendType, sendPayload);
        try
        {
            return await tcs.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(10));
        }
        catch
        {
            return fallback();
        }
        finally
        {
            Unregister(recvType, Handler);
        }
    }

    public void Register(PacketType type, Action<string> handler)
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = [];
                _handlers[type] = list;
            }

            list.Add(handler);
        }
    }

    public void Unregister(PacketType type, Action<string> handler)
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(type, out var list))
            {
                list.Remove(handler);
            }
        }
    }

    public void Dispatch(byte[] data)
    {
        var packet = PacketSerializer.Deserialize(data);
        if (packet is null)
        {
            logger.LogWarning("Could not deserialize incoming packet");
            return;
        }

        logger.LogDebug("Dispatching packet: {Type}", packet.Type);

        List<Action<string>>? handlers;
        lock (_lock)
        {
            _handlers.TryGetValue(packet.Type, out handlers);
            handlers = handlers?.ToList(); // snapshot to avoid holding lock during invoke
        }

        if (handlers is not null)
        {
            foreach (var handler in handlers)
            {
                try
                {
                    handler(packet.Payload);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Handler error for packet {Type}", packet.Type);
                }
            }
        }
        else
        {
            logger.LogDebug("No handler for {Type}", packet.Type);
        }
    }
}