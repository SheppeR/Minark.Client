using Minark.Server.Data.Entities;

// ReSharper disable UnusedMemberInSuper.Global

namespace Minark.Server.Services.Interfaces;

public interface IAuthService
{
    Task<ChallengeResponse> GetChallengeAsync(string username);
    Task<LoginResponse> LoginAsync(LoginRequest request, string clientGuid);
    Task<AckResponse> RegisterAsync(RegisterRequest request);
    Task<AckResponse> LogoutAsync(string token);
    Task<Session?> ValidateTokenAsync(string token);
    Task InvalidateClientSessionsAsync(string clientGuid);
}