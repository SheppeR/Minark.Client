using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minark.Server.Data.Entities;

[Table("chat_messages")]
public class ChatMessage
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("sender_id")]
    public int SenderId { get; init; }

    [Column("receiver_id")]
    public int ReceiverId { get; init; }

    [Column("content")]
    // ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
    public string Content { get; set; } = string.Empty;

    [Column("sent_at")]
    public DateTime SentAt { get; init; } = DateTime.UtcNow;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("is_edited")]
    public bool IsEdited { get; set; }

    [ForeignKey("SenderId")]
    public User Sender { get; init; } = null!;

    [ForeignKey("ReceiverId")]
    public User Receiver { get; init; } = null!;

    public ICollection<MessageReaction> Reactions { get; init; } = new List<MessageReaction>();
}