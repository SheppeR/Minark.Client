namespace Minark.Shared.Packets.Chat;

public class TypingNotification
{
    public string FromUsername { get; set; } = string.Empty;
    public string ToUsername { get; init; } = string.Empty;
}