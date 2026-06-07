namespace Minark.Server.Services.Interfaces;

public interface ILoginRateLimiter
{
    bool IsLimited(string username);
    void RecordFailure(string username);
    void Reset(string username);
}