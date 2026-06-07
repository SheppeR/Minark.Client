namespace Minark.Game.Shared.Packets.Auth;

public class AuthRequest
{
    /// <summary>Token généré par Minark.Server au login du launcher.</summary>
    public string Token { get; set; } = string.Empty;
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public GameUserDto? User { get; set; }
}