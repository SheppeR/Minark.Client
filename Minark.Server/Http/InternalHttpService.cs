using System.Net;
using System.Text.Json;
using Minark.Server.Services.Interfaces;

namespace Minark.Server.Http;

/// <summary>
///     Micro-serveur HTTP interne écoutant sur toutes les interfaces.
///     Reçoit les notifications du GameServer (joueur entré/sorti du jeu)
///     et délègue le push TCP aux amis via <see cref="IServerSender" />.
///     Endpoint : POST http://+:{port}/internal/player-status
///     Header   : X-Internal-Key: {clé partagée}
/// </summary>
public sealed class InternalHttpService(
    IConfiguration config,
    IServerSender sender,
    ISessionStore sessionStore,
    IServiceScopeFactory scopeFactory,
    ILogger<InternalHttpService> log)
    : IHostedService, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpListener _listener = new();

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        _cts?.Dispose();
        _listener.Close();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var port = config.GetValue("Internal:HttpPort", 9001);

        _listener.Prefixes.Add($"http://+:{port}/");
        _listener.Start();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);

        log.LogInformation("InternalHttpService démarré sur http://*:{Port}/", port);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cts?.CancelAsync();
        _listener.Stop();

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                /* normal */
            }
        }
    }

    // ── Boucle d'acceptation ──────────────────────────────────────────────────

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "InternalHttpService: erreur lors de l'acceptation de la connexion");
                continue;
            }

            _ = Task.Run(() => HandleRequestAsync(ctx, ct), ct);
        }
    }

    // ── Traitement d'une requête ──────────────────────────────────────────────

    private async Task HandleRequestAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        var req = ctx.Request;
        var res = ctx.Response;

        try
        {
            // ── Route ─────────────────────────────────────────────────────────
            if (req.HttpMethod != "POST" ||
                !req.Url!.AbsolutePath.Equals("/internal/player-status", StringComparison.OrdinalIgnoreCase))
            {
                res.StatusCode = 404;
                res.Close();
                return;
            }

            // ── Auth clé partagée ─────────────────────────────────────────────
            var expectedKey = config["Internal:SharedKey"];
            var receivedKey = req.Headers["X-Internal-Key"];

            if (string.IsNullOrWhiteSpace(expectedKey) || receivedKey != expectedKey)
            {
                log.LogWarning("InternalHttpService: clé invalide reçue depuis {Endpoint}", req.RemoteEndPoint);
                res.StatusCode = 401;
                res.Close();
                return;
            }

            // ── Désérialisation ───────────────────────────────────────────────
            InternalStatusRequest? body;
            try
            {
                body = await JsonSerializer.DeserializeAsync<InternalStatusRequest>(
                    req.InputStream, JsonOpts, ct);
            }
            catch
            {
                res.StatusCode = 400;
                res.Close();
                return;
            }

            if (body is null || string.IsNullOrWhiteSpace(body.Token))
            {
                res.StatusCode = 400;
                res.Close();
                return;
            }

            // ── Résolution token → userId + username via DB ───────────────────
            await using var scope = scopeFactory.CreateAsyncScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

            var session = await authService.ValidateTokenAsync(body.Token);
            if (session is null)
            {
                log.LogWarning("InternalHttpService: token invalide/expiré reçu du GameServer");
                res.StatusCode = 404;
                res.Close();
                return;
            }

            // ── Mise à jour statut en DB ──────────────────────────────────────
            var friendService = scope.ServiceProvider.GetRequiredService<IFriendService>();
            await friendService.UpdateStatusAsync(session.UserId, body.Status);

            // ── Push TCP aux amis connectés au launcher ───────────────────────
            await sender.PushStatusToFriendsAsync(session.UserId, session.User.Username, body.Status);

            // ── Push TCP à soi-même ───────────────────────────────────────────
            var selfGuid = sessionStore.FindClientByUserId(session.UserId);
            if (selfGuid is not null)
            {
                await sender.SendAsync(selfGuid.Value, PacketType.SelfStatusUpdate,
                    new SelfStatusUpdate { Status = body.Status });
            }

            log.LogInformation(
                "InternalHttpService: statut {Status} appliqué pour {Username} ({UserId})",
                body.Status, session.User.Username, session.UserId);

            res.StatusCode = 200;
            res.Close();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "InternalHttpService: erreur inattendue");
            try
            {
                res.StatusCode = 500;
                res.Close();
            }
            catch
            {
                /* déjà fermé */
            }
        }
    }
}