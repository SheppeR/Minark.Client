using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minark.Server.Data.Entities;

[Table("unread_messages")]
public class UnreadMessage
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    /// <summary>L'utilisateur qui doit lire ce message.</summary>
    [Column("recipient_id")]
    public int RecipientId { get; init; }

    /// <summary>Référence au message original.</summary>
    [Column("message_id")]
    public int MessageId { get; init; }

    [Column("from_username")]
    [MaxLength(50)]
    public string FromUsername { get; init; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [ForeignKey("RecipientId")]
    public User Recipient { get; init; } = null!;

    [ForeignKey("MessageId")]
    public ChatMessage Message { get; init; } = null!;
}