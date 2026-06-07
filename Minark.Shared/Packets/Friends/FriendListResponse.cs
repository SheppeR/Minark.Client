namespace Minark.Shared.Packets.Friends;

public class FriendListResponse
{
    public bool Success { get; set; }
    public List<FriendDto> Friends { get; init; } = [];
    public List<PendingInviteDto> PendingInvites { get; init; } = [];
    public string? ErrorMessage { get; set; }
}