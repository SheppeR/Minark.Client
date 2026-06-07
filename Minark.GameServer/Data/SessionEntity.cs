namespace Minark.GameServer.Data;

// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength
[Table("sessions")]
public class SessionEntity
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("user_id")]
    public int UserId { get; init; }

    [Column("token")]
    public string Token { get; init; } = string.Empty;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; init; }

    public UserEntity User { get; init; } = null!;
}