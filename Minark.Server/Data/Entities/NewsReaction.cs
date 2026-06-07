using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minark.Server.Data.Entities;

[Table("news_reactions")]
public class NewsReaction
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("news_id")]
    public int NewsId { get; init; }

    [Column("user_id")]
    public int UserId { get; init; }

    [Column("reaction")]
    public int Reaction { get; set; } // 1=Like, 2=Dislike

    [ForeignKey("NewsId")]
    public News News { get; init; } = null!;

    [ForeignKey("UserId")]
    public User User { get; init; } = null!;
}