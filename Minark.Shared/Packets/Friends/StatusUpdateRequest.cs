namespace Minark.Shared.Packets.Friends;

/// <summary>Envoyé par le client pour changer son propre statut.</summary>
public class StatusUpdateRequest
{
    public string Token { get; init; } = string.Empty;
    public UserStatus Status { get; init; }
}