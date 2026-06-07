using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Minark.Server.Data;
using Minark.Server.Data.Entities;
using Minark.Server.Services.Interfaces;

namespace Minark.Server.Services;

public class AuthService(
    AppDbContext db,
    ILogger<AuthService> logger,
    IChallengeStore challenges,
    ILoginRateLimiter rateLimiter)
    : IAuthService
{
    public Task<ChallengeResponse> GetChallengeAsync(string username)
    {
        var nonce = challenges.Issue(username);
        logger.LogDebug("Challenge issued for {Username}", username);
        return Task.FromResult(new ChallengeResponse { Nonce = nonce });
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string clientGuid)
    {
        if (rateLimiter.IsLimited(request.Username))
        {
            logger.LogWarning("Rate limit reached for {Username}", request.Username);
            return new LoginResponse
                { Success = false, ErrorMessage = "Trop de tentatives. Réessayez dans une minute." };
        }

        if (!challenges.TryConsume(request.Username, out var entry))
        {
            return new LoginResponse { Success = false, ErrorMessage = "Challenge introuvable. Recommencez." };
        }

        if (DateTime.UtcNow > entry.Expiry)
        {
            return new LoginResponse { Success = false, ErrorMessage = "Challenge expiré. Recommencez." };
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user is null)
        {
            return new LoginResponse { Success = false, ErrorMessage = "Utilisateur introuvable." };
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            rateLimiter.RecordFailure(request.Username);
            return new LoginResponse { Success = false, ErrorMessage = "Mot de passe incorrect." };
        }

        // Vérification HMAC (défense en profondeur)
        var expectedHmac = ComputeHmac(request.Password, entry.Nonce);
        if (!string.IsNullOrEmpty(request.Hmac) &&
            !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(request.Hmac),
                Convert.FromHexString(expectedHmac)))
        {
            logger.LogWarning("HMAC mismatch for {Username}", request.Username);
            return new LoginResponse { Success = false, ErrorMessage = "Vérification HMAC échouée." };
        }

        // Login réussi — réinitialiser le compteur
        rateLimiter.Reset(request.Username);

        await InvalidateClientSessionsAsync(clientGuid);

        var token = GenerateToken();
        var session = new Session
        {
            UserId = user.Id,
            Token = token,
            ClientGuid = clientGuid,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        user.Status = (int)UserStatus.Online;
        user.LastSeenAt = DateTime.UtcNow;
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        logger.LogInformation("User {Username} logged in", user.Username);
        return new LoginResponse
        {
            Success = true,
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Status = (UserStatus)user.Status,
                CreatedAt = user.CreatedAt,
                AvatarUrl = user.AvatarUrl
            }
        };
    }

    public async Task<AckResponse> RegisterAsync(RegisterRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Username == request.Username))
        {
            return new AckResponse { Success = false, ErrorMessage = "Ce nom d'utilisateur est déjà pris." };
        }

        if (await db.Users.AnyAsync(u => u.Email == request.Email))
        {
            return new AckResponse { Success = false, ErrorMessage = "Cet email est déjà utilisé." };
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return new AckResponse
                { Success = false, ErrorMessage = "Le mot de passe doit faire au moins 8 caractères." };
        }

        db.Users.Add(new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        logger.LogInformation("New user registered: {Username}", request.Username);
        return new AckResponse { Success = true };
    }

    public async Task<AckResponse> LogoutAsync(string token)
    {
        var session = await db.Sessions.Include(s => s.User).FirstOrDefaultAsync(s => s.Token == token);
        if (session is null)
        {
            return new AckResponse { Success = false };
        }

        session.User.Status = (int)UserStatus.Offline;
        db.Sessions.Remove(session);
        await db.SaveChangesAsync();
        logger.LogInformation("User {Username} logged out", session.User.Username);
        return new AckResponse { Success = true };
    }

    public async Task<Session?> ValidateTokenAsync(string token)
    {
        return await db.Sessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Token == token && s.ExpiresAt > DateTime.UtcNow);
    }

    public async Task InvalidateClientSessionsAsync(string clientGuid)
    {
        var sessions = db.Sessions.Where(s => s.ClientGuid == clientGuid);
        db.Sessions.RemoveRange(sessions);
        await db.SaveChangesAsync();
    }

    private static string ComputeHmac(string password, string nonce)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var data = Encoding.UTF8.GetBytes(nonce);
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(data)).ToLowerInvariant();
    }

    private static string GenerateToken()
    {
        var bytes = new byte[96];
        RandomNumberGenerator.Fill(bytes);
        var raw = Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
        return raw.Length >= 64 ? raw[..64] : raw.PadRight(64, '0');
    }
}