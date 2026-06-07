using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minark.Server.Data.Entities;

[Table("news_comments")]
public class NewsComment
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("news_id")]
    public int NewsId { get; init; }

    [Column("user_id")]
    public int UserId { get; init; }

    [Column("content")]
    [MaxLength(1000)]
    public string Content { get; init; } = string.Empty;

    [Column("posted_at")]
    public DateTime PostedAt { get; init; } = DateTime.UtcNow;

    [ForeignKey("NewsId")]
    public News News { get; init; } = null!;

    [ForeignKey("UserId")]
    public User User { get; init; } = null!;
}