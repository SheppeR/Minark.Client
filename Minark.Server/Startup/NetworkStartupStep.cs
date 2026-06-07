using System.Threading.Channels;
using Minark.Server.Networking;
using Minark.Server.Services.Interfaces;
using Minark.Shared;
using WatsonTcp;

namespace Minark.Server.Startup;

/// <summary>
///     Étape 2 — Réseau TCP (WatsonTcp).
///     Enregistre les événements, démarre le serveur et lance la boucle de traitement des paquets.
///     (Extrait de <c>TcpServerHostedService</c> pour contrôler l'ordre de démarrage.)
/// </summary>
public class NetworkStartupStep : IStartupStep, IAsyncDisposable
{
    private readonly IConfiguration _config;

    private readonly Channel<(Guid ClientGuid, byte[] Data)> _incomingChannel =
        Channel.CreateBounded<(Guid, byte[])>(new BoundedChannelOptions(10_000)
        {
            SingleReader = true,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private readonly ILogger<NetworkStartupStep> _log;
    private readonly PacketRouter _packetRouter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WatsonTcpServer _server;
    private readonly IServerSender _serverSender;
    private readonly ISessionStore _sessionStore;

    private CancellationTokenSource? _cts;
    private Task? _processingTask;

    public NetworkStartupStep(
        WatsonTcpServer server,
        PacketRouter packetRouter,
        IServerSender serverSender,
        ISessionStore sessionStore,
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<NetworkStartupStep> log)
    {
        _server = server;
        _packetRouter = packetRouter;
        _serverSender = serverSender;
        _sessionStore = sessionStore;
        _scopeFactory = scopeFactory;
        _config = config;
        _log = log;

        _server.Events.ClientConnected += OnClientConnected;
        _server.Events.ClientDisconnected += OnClientDisconnected;
        _server.Events.MessageReceived += OnMessageReceived;
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Dispose();
        if (_processingTask is not null)
        {
            await _processingTask.ConfigureAwait(false);
        }
    }

    public string Name => "Network";
    public int Order => StartupOrder.Network;

    public Task ExecuteAsync(CancellationToken ct)
    {
        SerilogUtils.PrintSection("NETWORK");

        var host = _config["Server:Host"] ?? "127.0.0.1";
        var port = int.Parse(_config["Server:Port"] ?? "9000");

        _log.LogInformation("Démarrage du serveur TCP sur {Host}:{Port}...", host, port);
        _server.Start();
        _log.LogInformation("TCP Server en écoute sur {Host}:{Port}", host, port);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _processingTask = ProcessIncomingAsync(_cts.Token);

        return Task.CompletedTask;
    }

    // ── Arrêt propre ─────────────────────────────────────────────────────────

    public async Task StopAsync(CancellationToken ct)
    {
        _server.Stop();
        _incomingChannel.Writer.TryComplete();

        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_processingTask is not null)
        {
            try
            {
                await _processingTask.WaitAsync(TimeSpan.FromSeconds(5), ct);
            }
            catch (OperationCanceledException)
            {
                /* arrêt forcé OK */
            }
        }

        _log.LogInformation("TCP Server arrêté.");
    }

    // ── Événements WatsonTcp ─────────────────────────────────────────────────

    private void OnClientConnected(object? sender, ConnectionEventArgs e)
    {
        _log.LogInformation("+ Client connecté    [{Guid}]", e.Client.Guid);
    }

    private void OnClientDisconnected(object? sender, DisconnectionEventArgs e)
    {
        var user = _sessionStore.GetUser(e.Client.Guid);
        _sessionStore.RemoveClient(e.Client.Guid);
        _log.LogInformation("- Client déconnecté  [{Guid}] ({Reason})", e.Client.Guid, e.Reason);

        if (user is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var friends = scope.ServiceProvider.GetRequiredService<IFriendService>();
                await friends.UpdateStatusAsync(user.Value.UserId, UserStatus.Offline);
                await _serverSender.PushStatusToFriendsAsync(
                    user.Value.UserId, user.Value.Username, UserStatus.Offline);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Erreur lors du push offline pour {User}", user.Value.Username);
            }
        });
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        _incomingChannel.Writer.TryWrite((e.Client.Guid, e.Data));
    }

    // ── Boucle de traitement ─────────────────────────────────────────────────

    private async Task ProcessIncomingAsync(CancellationToken ct)
    {
        await foreach (var (clientGuid, data) in _incomingChannel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await _packetRouter.HandleAsync(clientGuid, data);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Erreur non gérée pour le paquet de {Client}", clientGuid);
            }
        }
    }
}