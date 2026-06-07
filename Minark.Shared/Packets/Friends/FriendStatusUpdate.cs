namespace Minark.Shared.Packets.Friends;

public class FriendStatusUpdate
{
    public string Username { get; init; } = string.Empty;
    public UserStatus Status { get; init; }
}