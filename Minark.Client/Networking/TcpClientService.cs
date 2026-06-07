using System.Windows;
using Minark.Shared.Packets;
using WatsonTcp;
using Timer = System.Timers.Timer;

namespace Minark.Client.Networking;

public class TcpClientService : IAsyncDisposable
{
    private readonly WatsonTcpClient _client;
    private readonly ILogger<TcpClientService> _logger;
    private readonly Timer _pingTimer;
    private readonly Timer _reconnectTimer;
    private bool _intentionalDisconnect;
    private int _reconnectAttempt;

    public TcpClientService(string host, int port, ILogger<TcpClientService> logger)
    {
        _logger = logger;

        // TLS strict : toujours activé, certificat serveur toujours validé.
        // Aucune option de configuration n'autorise un relâchement — c'est volontaire.
        _logger.LogInformation("Connecting to {Host}:{Port} over TLS", host, port);
        _client = new WatsonTcpClient(host, port, "", "");
        _client.Settings.AcceptInvalidCertificates = false;
        _client.Settings.MutuallyAuthenticate = false;

        _client.Events.ServerConnected += (_, _) =>
        {
            _reconnectAttempt = 0;
            _reconnectTimer.Stop();
            _logger.LogInformation("Connected to server {Host}:{Port}", host, port);
            _pingTimer.Start();
            Dispatch(() => OnConnected?.Invoke());
        };

        _client.Events.ServerDisconnected += (_, _) =>
        {
            _pingTimer.Stop();
            _logger.LogWarning("Disconnected from server");
            Dispatch(() => OnDisconnected?.Invoke());

            if (!_intentionalDisconnect)
            {
                ScheduleReconnect();
            }
        };

        _client.Events.MessageReceived += (_, e) => OnDataReceived?.Invoke(e.Data);

        _pingTimer = new Timer(30_000) { AutoReset = true };
        _pingTimer.Elapsed += async (_, _) =>
        {
            if (!_client.Connected)
            {
                _pingTimer.Stop();
                Dispatch(() => OnDisconnected?.Invoke());
                return;
            }

            try
            {
                await _client.SendAsync(PacketSerializer.Serialize(PacketType.Ping, new { }));
            }
            catch
            {
                _pingTimer.Stop();
                Dispatch(() => OnDisconnected?.Invoke());
            }
        };

        // Reconnect timer — fires once after delay, then reschedules itself
        _reconnectTimer = new Timer { AutoReset = false };
        _reconnectTimer.Elapsed += async (_, _) => await TryReconnectAsync();
    }

    public bool IsConnected => _client.Connected;

    public ValueTask DisposeAsync()
    {
        _intentionalDisconnect = true;
        _pingTimer.Dispose();
        _reconnectTimer.Dispose();
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    public event Action<byte[]>? OnDataReceived;
    public event Action? OnConnected;
    public event Action? OnDisconnected;
    public event Action? OnConnectionFailed;

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        const int maxRetries = 5;
        const int delayMs = 2000;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _client.Connect();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Connect attempt {A}/{M} failed: {Msg}", attempt, maxRetries, ex.Message);
                if (attempt < maxRetries)
                {
                    await Task.Delay(delayMs, ct);
                }
            }
        }

        _logger.LogError("Could not connect after {Max} attempts", maxRetries);
        Dispatch(() => OnConnectionFailed?.Invoke());
        return false;
    }

    public async Task SendAsync<T>(PacketType type, T payload)
    {
        if (!_client.Connected)
        {
            _logger.LogWarning("Cannot send — not connected");
            return;
        }

        var data = PacketSerializer.Serialize(type, payload);
        await _client.SendAsync(data);
    }

    public void Disconnect()
    {
        _intentionalDisconnect = true;
        _reconnectTimer.Stop();
        _pingTimer.Stop();
        try
        {
            _client.Disconnect();
        }
        catch
        {
            /* ignore */
        }
    }

    private void ScheduleReconnect()
    {
        _reconnectAttempt++;
        // Exponential backoff: 2s, 4s, 8s, 16s, max 30s
        var delayMs = Math.Min(2000 * (int)Math.Pow(2, _reconnectAttempt - 1), 30_000);
        _logger.LogInformation("Reconnect attempt {N} in {Delay}ms", _reconnectAttempt, delayMs);
        _reconnectTimer.Interval = delayMs;
        _reconnectTimer.Start();
    }

    private Task TryReconnectAsync()
    {
        if (_intentionalDisconnect || _client.Connected)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Attempting reconnect #{N}...", _reconnectAttempt);
        try
        {
            _client.Connect();
            // Success handled by ServerConnected event
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Reconnect #{N} failed: {Msg}", _reconnectAttempt, ex.Message);
            ScheduleReconnect();
        }

        return Task.CompletedTask;
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.HasShutdownStarted)
        {
            try
            {
                dispatcher.Invoke(action);
            }
            catch (TaskCanceledException)
            {
                // Application is shutting down
            }
        }
        else
        {
            action();
        }
    }
}