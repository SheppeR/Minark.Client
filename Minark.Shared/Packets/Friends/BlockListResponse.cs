namespace Minark.Shared.Packets.Friends;

public class BlockListResponse
{
    public bool Success { get; init; }
    public List<string> BlockedUsernames { get; init; } = [];
}