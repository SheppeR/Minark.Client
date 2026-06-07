namespace Minark.Server.Services.Interfaces;

public interface IChallengeStore
{
    string Issue(string username);
    bool TryConsume(string username, out (string Nonce, DateTime Expiry) entry);
}