namespace Minark.Shared.Packets;

/// <summary>
///     Requête générique ne portant qu'un token d'authentification.
///     Remplace : LogoutRequest, FriendListRequest, BlockListRequest, UnreadCountsRequest.
/// </summary>
public class TokenRequest
{
    public string Token { get; init; } = string.Empty;
}