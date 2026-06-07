namespace Minark.Shared.Packets;

public class PendingInviteDto
{
    public int FriendshipId { get; init; }
    public string FromUsername { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}