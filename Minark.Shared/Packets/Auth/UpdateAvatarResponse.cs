namespace Minark.Shared.Packets.Auth;

public class UpdateAvatarResponse
{
    public bool Success { get; init; }
    public string? AvatarUrl { get; init; }
    public string? ErrorMessage { get; init; }
}