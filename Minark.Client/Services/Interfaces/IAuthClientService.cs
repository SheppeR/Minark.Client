using Minark.Shared.Packets;
using Minark.Shared.Packets.Auth;

namespace Minark.Client.Services.Interfaces;

public interface IAuthClientService
{
    UserDto? CurrentUser { get; }
    string? Token { get; }
    bool IsLoggedIn { get; }
    event Action<string>? AvatarChanged;
    event Action<UserDto>? LoggedIn;

    /// <summary>
    ///     Déclenché quand le serveur nous notifie que notre session a été invalidée,
    ///     typiquement parce que le compte s'est reconnecté depuis un autre appareil.
    ///     L'argument contient le motif affichable à l'utilisateur.
    /// </summary>
    event Action<string>? SessionInvalidated;

    void NotifyAvatarChanged(string url);

    Task<LoginResponse> LoginAsync(string username, string password);
    Task<AckResponse> RegisterAsync(string username, string email, string password);
    Task<AckResponse> LogoutAsync();
}