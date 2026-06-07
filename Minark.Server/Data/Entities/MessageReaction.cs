using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minark.Server.Data.Entities;

[Table("message_reactions")]
public class MessageReaction
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("message_id")]
    public int MessageId { get; init; }

    [Column("user_id")]
    public int UserId { get; init; }

    [Column("emoji")]
    [MaxLength(32)]
    public string Emoji { get; init; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [ForeignKey("MessageId")]
    public ChatMessage Message { get; init; } = null!;

    [ForeignKey("UserId")]
    public User User { get; init; } = null!;
}