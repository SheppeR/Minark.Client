using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Minark.Server.Data.Entities;

[Table("news")]
public class News
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("title")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("author")]
    [MaxLength(100)]
    public string Author { get; set; } = string.Empty;

    [Column("category")]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Column("image_url")]
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [Column("published_at")]
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    [Column("is_published")]
    public bool IsPublished { get; set; } = true;
}