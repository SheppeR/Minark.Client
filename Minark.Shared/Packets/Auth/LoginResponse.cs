namespace Minark.Shared.Packets.Auth;

public class LoginResponse
{
    public bool Success { get; init; }
    public string? Token { get; init; }
    public string? ErrorMessage { get; init; }
    public UserDto? User { get; init; }
}