namespace Minark.GameServer.Data;

// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength
[Table("users")]
public class UserEntity
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("username")]
    public string Username { get; init; } = string.Empty;

    [Column("status")]
    public int Status { get; init; }

    [Column("avatar_url")]
    public string? AvatarUrl { get; init; }

    [Column("is_admin")]
    public bool IsAdmin { get; init; }
}