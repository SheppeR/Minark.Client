namespace Minark.Shared.Packets.Friends;

public class FriendRequestSend
{
    public string Token { get; init; } = string.Empty;
    public string TargetUsername { get; init; } = string.Empty;
}