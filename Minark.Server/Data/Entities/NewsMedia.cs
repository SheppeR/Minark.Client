using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Minark.Server.Data.Entities;

[Table("news_media")]
public class NewsMedia
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("news_id")]
    public int NewsId { get; init; }

    [Column("url")]
    public string Url { get; init; } = string.Empty;

    [Column("media_type")]
    [MaxLength(10)]
    public string MediaType { get; init; } = "image"; // "image" | "video"

    [Column("sort_order")]
    public int SortOrder { get; init; }

    [ForeignKey("NewsId")]
    public News News { get; init; } = null!;
}