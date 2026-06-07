namespace Minark.Shared.Packets.Chat;

/// <summary>Client marque les messages d'une conversation comme lus.</summary>
public class MarkAsReadRequest
{
    public string Token { get; init; } = string.Empty;
    public string FromUsername { get; init; } = string.Empty;
}