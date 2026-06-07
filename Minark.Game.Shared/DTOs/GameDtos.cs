namespace Minark.Game.Shared.DTOs;

public class GameUserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsAdmin { get; set; }
}

public class GameFriendDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public GameUserStatus Status { get; set; }
}

public enum GameUserStatus : byte
{
    Offline = 0,
    Online = 1,
    InGame = 2,
    Away = 3,
    DoNotDisturb = 4
}