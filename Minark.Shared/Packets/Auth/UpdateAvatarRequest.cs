namespace Minark.Shared.Packets.Auth;

public class UpdateAvatarRequest
{
    public string Token { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
}