using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minark.Server.Data.Entities;

[Table("sessions")]
public class Session
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("user_id")]
    public int UserId { get; init; }

    [Column("token")]
    [MaxLength(128)]
    public string Token { get; init; } = string.Empty;

    [Column("client_guid")]
    [MaxLength(36)]
    public string ClientGuid { get; init; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; init; }

    [ForeignKey("UserId")]
    public User User { get; init; } = null!;
}