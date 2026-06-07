using Microsoft.Extensions.Caching.Memory;
using Minark.Server.Data.Entities;

namespace Minark.Server.Services;

/// <summary>
///     Cache les sessions valides en mémoire pour éviter les requêtes DB répétées.
///     Invalidation automatique après 5 minutes.
/// </summary>
public interface ITokenCacheService
{
    void Set(string token, Session session);
    bool TryGet(string token, out Session? session);
    void Invalidate(string token);
}

public class TokenCacheService(IMemoryCache cache, ILogger<TokenCacheService> logger) : ITokenCacheService
{
    private const int CACHE_DURATION_SECONDS = 300; // 5 minutes

    public void Set(string token, Session session)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CACHE_DURATION_SECONDS),
            SlidingExpiration = TimeSpan.FromSeconds(60)
        };

        cache.Set($"token:{token}", session, cacheOptions);
        logger.LogDebug("Cached token for user {UserId}", session.UserId);
    }

    public bool TryGet(string token, out Session? session)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            session = null;
            return false;
        }

        var found = cache.TryGetValue($"token:{token}", out session);
        if (found)
        {
            logger.LogDebug("Token cache hit for user {UserId}", session?.UserId);
        }

        return found;
    }

    public void Invalidate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        cache.Remove($"token:{token}");
        logger.LogInformation("Invalidated token cache");
    }
}