using System.Security.Cryptography;
using System.Text;
using Minark.Client.Networking;
using Minark.Client.Services.Interfaces;
using Minark.Shared.Packets;
using Minark.Shared.Packets.Auth;

namespace Minark.Client.Services;

public class AuthClientService : IAuthClientService
{
    private readonly PacketDispatcher _dispatcher;
    private readonly ILogger<AuthClientService> _logger;
    private readonly TcpClientService _tcp;

    public AuthClientService(TcpClientService tcp, PacketDispatcher dispatcher, ILogger<AuthClientService> logger)
    {
        _tcp = tcp;
        _dispatcher = dispatcher;
        _logger = logger;

        _dispatcher.Register(PacketType.SessionInvalidated, payload =>
        {
            var notif = PacketSerializer.DeserializePayload<SessionInvalidatedNotification>(payload);
            var reason = string.IsNullOrWhiteSpace(notif?.Reason)
                ? "Votre session a été invalidée."
                : notif.Reason;

            _logger.LogWarning("Session invalidated by server: {Reason}", reason);
            CurrentUser = null;
            Token = null;
            SessionInvalidated?.Invoke(reason);
        });
    }

    public UserDto? CurrentUser { get; private set; }
    public string? Token { get; private set; }
    public bool IsLoggedIn => Token is not null && CurrentUser is not null;

    public event Action<string>? AvatarChanged;
    public event Action<string>? SessionInvalidated;
    public event Action<UserDto>? LoggedIn;

    public void NotifyAvatarChanged(string url)
    {
        AvatarChanged?.Invoke(url);
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var nonce = await RequestChallengeAsync(username);
        if (nonce is null)
        {
            return new LoginResponse
                { Success = false, ErrorMessage = "Impossible d'obtenir un challenge du serveur." };
        }

        var hmac = ComputeHmac(password, nonce);
        var result = await _dispatcher.RequestStructAsync(
            _tcp,
            PacketType.LoginRequest, new LoginRequest { Username = username, Password = password, Hmac = hmac },
            PacketType.LoginResponse,
            () => new LoginResponse { Success = false, ErrorMessage = "Le serveur ne répond pas (timeout)." });

        if (result.Success)
        {
            CurrentUser = result.User;
            Token = result.Token;
            _logger.LogInformation("Logged in as {Username}", CurrentUser?.Username);
            if (CurrentUser is not null)
            {
                LoggedIn?.Invoke(CurrentUser);
            }
        }

        return result;
    }

    public Task<AckResponse> RegisterAsync(string username, string email, string password)
    {
        return _dispatcher.RequestStructAsync(
            _tcp,
            PacketType.RegisterRequest, new RegisterRequest { Username = username, Email = email, Password = password },
            PacketType.RegisterResponse,
            () => new AckResponse { Success = false, ErrorMessage = "Le serveur ne répond pas." });
    }

    public async Task<AckResponse> LogoutAsync()
    {
        if (Token is null)
        {
            return new AckResponse { Success = false };
        }

        var result = await _dispatcher.RequestStructAsync(
            _tcp,
            PacketType.LogoutRequest, new TokenRequest { Token = Token },
            PacketType.LogoutResponse,
            () => new AckResponse { Success = false });

        if (result.Success)
        {
            CurrentUser = null;
            Token = null;
        }

        return result;
    }

    private Task<string?> RequestChallengeAsync(string username)
    {
        var tcs = new TaskCompletionSource<string?>();

        void Handler(string payload)
        {
            var r = PacketSerializer.DeserializePayload<ChallengeResponse>(payload);
            tcs.TrySetResult(string.IsNullOrWhiteSpace(r?.Nonce) ? null : r.Nonce);
        }

        _dispatcher.Register(PacketType.ChallengeResponse, Handler);
        _ = _tcp.SendAsync(PacketType.ChallengeRequest, new ChallengeRequest { Username = username });
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(10))
            .ContinueWith(t =>
            {
                _dispatcher.Unregister(PacketType.ChallengeResponse, Handler);
                if (t.IsFaulted)
                {
                    _logger.LogWarning("Challenge timeout for {U}", username);
                    return null;
                }

                return t.Result;
            });
    }

    private static string ComputeHmac(string password, string nonce)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(nonce))).ToLowerInvariant();
    }
}