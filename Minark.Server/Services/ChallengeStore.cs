using System.Collections.Concurrent;
using System.Security.Cryptography;
using Minark.Server.Services.Interfaces;

namespace Minark.Server.Services;

public class ChallengeStore : IChallengeStore
{
    private readonly ConcurrentDictionary<string, (string Nonce, DateTime Expiry)> _challenges = new();

    public string Issue(string username)
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        _challenges[username] = (nonce, DateTime.UtcNow.AddSeconds(60));
        return nonce;
    }

    public bool TryConsume(string username, out (string Nonce, DateTime Expiry) entry)
    {
        return _challenges.TryRemove(username, out entry);
    }
}