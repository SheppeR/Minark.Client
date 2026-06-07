using System.Windows;
using Minark.Client.Networking;
using Minark.Client.Services.Interfaces;
using Minark.Shared.Packets;

namespace Minark.Client.Services;

/// <summary>
///     Écoute les événements TCP et restaure silencieusement la session
///     utilisateur <strong>uniquement après une perte de connexion en cours d'utilisation</strong>.
///     <para>
///         Au tout premier login d'une instance, l'utilisateur doit cliquer "Se connecter"
///         explicitement via <see cref="Minark.Client.ViewModels.Pages.LoginViewModel" />.
///         Sans cette distinction, chaque lancement d'un nouveau client auto-restaurerait
///         la session du dernier compte sauvé — ce qui fait qu'une deuxième instance
///         vole le compte de la première.
///     </para>
/// </summary>
public class ReconnectionService
{
    private readonly IAuthClientService _auth;
    private readonly NotificationBadgeService _badge;
    private readonly IChatClientService _chat;
    private readonly ICredentialsService _credentials;
    private readonly IFriendClientService _friends;
    private readonly ILogger<ReconnectionService> _logger;
    private readonly TcpClientService _tcp;

    /// <summary>
    ///     Vrai dès qu'un login réussi a eu lieu dans cette instance. C'est seulement à partir
    ///     de ce moment-là que <see cref="ReconnectionService" /> tentera une re-authentification
    ///     silencieuse sur reconnexion TCP.
    /// </summary>
    private bool _hasLoggedInOnce;

    public ReconnectionService(
        TcpClientService tcp,
        IAuthClientService auth,
        ICredentialsService credentials,
        IFriendClientService friends,
        NotificationBadgeService badge,
        IChatClientService chat,
        ILogger<ReconnectionService> logger)
    {
        _tcp = tcp;
        _auth = auth;
        _credentials = credentials;
        _friends = friends;
        _badge = badge;
        _chat = chat;
        _logger = logger;

        _tcp.OnConnected += () => Dispatch(() =>
        {
            IsOfflineChanged?.Invoke(false);
            if (_hasLoggedInOnce)
            {
                _ = TryRestoreSessionAsync();
            }
        });

        _tcp.OnDisconnected += () => Dispatch(() =>
            IsOfflineChanged?.Invoke(true));
    }

    /// <summary>Levé sur le thread UI quand l'état offline change.</summary>
    public event Action<bool>? IsOfflineChanged;

    /// <summary>
    ///     À appeler après un login réussi piloté par l'utilisateur. Autorise
    ///     désormais les tentatives de restauration silencieuse sur reconnexion réseau.
    /// </summary>
    public void NotifyUserLoggedIn()
    {
        _hasLoggedInOnce = true;
    }

    /// <summary>
    ///     À appeler sur logout explicite ou session invalidée : on repasse en mode
    ///     "pas de restauration auto" tant que l'utilisateur n'a pas refait un login manuel.
    /// </summary>
    public void NotifyUserLoggedOut()
    {
        _hasLoggedInOnce = false;
    }

    private async Task TryRestoreSessionAsync()
    {
        var creds = _credentials.Load();
        if (!creds.RememberMe
            || string.IsNullOrWhiteSpace(creds.Username)
            || string.IsNullOrWhiteSpace(creds.Password))
        {
            return;
        }

        _logger.LogInformation("Restauration de session pour {User}...", creds.Username);
        try
        {
            var result = await _auth.LoginAsync(creds.Username, creds.Password);
            if (result.Success && _auth.Token is not null)
            {
                var status = _auth.CurrentUser?.Status ?? UserStatus.Online;
                await _friends.UpdateStatusAsync(_auth.Token, status);

                // Recharger les badges — ils ont pu évoluer pendant la déconnexion.
                _badge.ClearMessages();
                await _badge.LoadFromServerAsync(_auth.Token, _chat);

                _logger.LogInformation("Session restaurée pour {User}", creds.Username);
            }
            else
            {
                _logger.LogWarning("Échec restauration session {User}: {Err}",
                    creds.Username, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception restauration session {User}", creds.Username);
        }
    }

    private static void Dispatch(Action action)
    {
        var d = Application.Current?.Dispatcher;
        if (d is not null && !d.CheckAccess())
        {
            d.Invoke(action);
        }
        else
        {
            action();
        }
    }
}