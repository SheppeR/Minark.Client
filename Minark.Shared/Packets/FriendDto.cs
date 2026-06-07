namespace Minark.Shared.Packets;

public class FriendDto
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public UserStatus Status { get; init; }
    public string? AvatarUrl { get; init; }
    public DateTime FriendsSince { get; init; }
}