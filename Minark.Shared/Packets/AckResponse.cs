namespace Minark.Shared.Packets;

/// <summary>
///     Réponse générique success/error.
///     Remplace : LogoutResponse, BlockResponse, FriendRequestResponse,
///     ChangePasswordResponse, RegisterResponse.
/// </summary>
public class AckResponse
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Message { get; set; } // Message libre (ex: "Welcome, username!")
}