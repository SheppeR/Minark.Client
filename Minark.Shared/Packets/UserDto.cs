namespace Minark.Shared.Packets;

public class UserDto
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserStatus Status { get; init; }
    public DateTime CreatedAt { get; set; }
    public string? AvatarUrl { get; set; }
}