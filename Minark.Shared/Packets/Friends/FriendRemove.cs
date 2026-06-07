namespace Minark.Shared.Packets.Friends;

public class FriendRemove
{
    public string Token { get; init; } = string.Empty;
    public string FriendUsername { get; init; } = string.Empty;
}