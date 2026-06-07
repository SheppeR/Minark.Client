namespace Minark.Shared.Packets.Auth;

public class ChangePasswordResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}