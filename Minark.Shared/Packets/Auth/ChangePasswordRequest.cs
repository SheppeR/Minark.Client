namespace Minark.Shared.Packets.Auth;

public class ChangePasswordRequest
{
    public string Token { get; init; } = string.Empty;
    public string OldPasswordHash { get; init; } = string.Empty;
    public string NewPasswordHash { get; init; } = string.Empty;
}