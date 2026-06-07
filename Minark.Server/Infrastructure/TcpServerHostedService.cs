using System.Diagnostics;
using System.Threading.Channels;
using Minark.Server.Networking;
using Minark.Server.Services.Interfaces;
using Minark.Shared;
using WatsonTcp;

namespace Minark.Server.Infrastructure;

public class TcpServerHostedService : IHostedService
{
    private readonly IConfiguration _config;

    /// <summary>
    ///     Channel MPSC : WatsonTcp écrit les messages entrants (producteur),
    ///     la boucle ProcessIncomingAsync les consomme séquentiellement (consommateur).
    ///     Remplace l'async void OnMessageReceived qui avalait silencieusement les exceptions.
    /// </summary>
    private readonly Channel<(Guid ClientGuid, byte[] Data)> _incomingChannel =
        Channel.CreateBounded<(Guid, byte[])>(new BoundedChannelOptions(10_000)
        {
            SingleReader = true,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private readonly ILogger<TcpServerHostedService> _logger;
    private readonly PacketRouter _packetRouter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WatsonTcpServer _server;
    private readonly IServerSender _serverSender;
    private readonly ISessionStore _sessionStore;
    private readonly Stopwatch _sw;
    private CancellationTokenSource? _cts;

    private Task? _processingTask;

    public TcpServerHostedService(
        WatsonTcpServer server,
        PacketRouter packetRouter,
        IServerSender serverSender,
        ISessionStore sessionStore,
        ILogger<TcpServerHostedService> logger,
        IConfiguration config,
        IServiceScopeFactory scopeFactory, Stopwatch sw)
    {
        _server = server;
        _packetRouter = packetRouter;
        _serverSender = serverSender;
        _sessionStore = sessionStore;
        _logger = logger;
        _config = config;
        _scopeFactory = scopeFactory;
        _sw = sw;

        _server.Events.ClientConnected += OnClientConnected;
        _server.Events.ClientDisconnected += OnClientDisconnected;
        // Synchronous push dans le channel — jamais async void
        _server.Events.MessageReceived += OnMessageReceived;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        SerilogUtils.PrintSection("DATABASE");
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbInit = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await dbInit.InitializeAsync(cancellationToken);

        SerilogUtils.PrintSection("NETWORK");
        var host = _config["Server:Host"] ?? "127.0.0.1";
        var port = int.Parse(_config["Server:Port"] ?? "9000");
        _logger.LogInformation("Starting TCP server on {Host}:{Port}...", host, port);
        _server.Start();
        _logger.LogInformation("TCP Server listening on {Host}:{Port}", host, port);

        // Démarrer la boucle de traitement des paquets entrants
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _processingTask = ProcessIncomingAsync(_cts.Token);

        SerilogUtils.PrintSection("SERVER READY");
        _logger.LogInformation("Minark is ready to accept connections.");
        _sw.Stop();
        _logger.LogInformation("Server started in {Elapsed} ms ({Seconds:0.00}s)",
            _sw.ElapsedMilliseconds,
            _sw.Elapsed.TotalSeconds);
        SerilogUtils.PrintSection("SERVICES INFOS");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        SerilogUtils.PrintSection("SERVER STOPPING");
        _logger.LogInformation("Shutting down, draining connections...");
        _server.Stop();

        // Signaler la fin et attendre que la boucle se termine proprement
        _incomingChannel.Writer.TryComplete();
        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_processingTask is not null)
        {
            try
            {
                await _processingTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                /* arrêt forcé OK */
            }
        }

        _logger.LogInformation("TCP Server stopped.");
    }

    // ── Event handlers (synchrones — pas d'async void) ────────────────────────

    private void OnClientConnected(object? sender, ConnectionEventArgs e)
    {
        _logger.LogInformation("+ Client connected    [{Guid}]", e.Client.Guid);
    }

    private void OnClientDisconnected(object? sender, DisconnectionEventArgs e)
    {
        var user = _sessionStore.GetUser(e.Client.Guid);
        _sessionStore.RemoveClient(e.Client.Guid);
        _logger.LogInformation("- Client disconnected [{Guid}] ({Reason})", e.Client.Guid, e.Reason);

        if (user is not null)
        {
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
                    _logger.LogError(ex, "Error pushing offline status for {User}", user.Value.Username);
                }
            });
        }
    }

    /// <summary>
    ///     Synchrone : pousse simplement le message dans le channel.
    ///     La boucle ProcessIncomingAsync le traite dans son propre contexte avec gestion d'erreur.
    /// </summary>
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
                _logger.LogError(ex, "Unhandled error processing packet from {Client}", clientGuid);
            }
        }
    }
}