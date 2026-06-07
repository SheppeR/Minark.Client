using System.Collections.Concurrent;
using Minark.Server.Services.Interfaces;

namespace Minark.Server.Services;

public class LoginRateLimiter : ILoginRateLimiter
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _attempts = new();

    public bool IsLimited(string username)
    {
        if (!_attempts.TryGetValue(username, out var entry))
        {
            return false;
        }

        if (DateTime.UtcNow - entry.WindowStart > AttemptWindow)
        {
            _attempts.TryRemove(username, out _);
            return false;
        }

        return entry.Count >= MaxAttempts;
    }

    public void RecordFailure(string username)
    {
        var now = DateTime.UtcNow;
        _attempts.AddOrUpdate(username,
            _ => (1, now),
            (_, existing) => now - existing.WindowStart > AttemptWindow
                ? (1, now)
                : (existing.Count + 1, existing.WindowStart));
    }

    public void Reset(string username)
    {
        _attempts.TryRemove(username, out _);
    }
}