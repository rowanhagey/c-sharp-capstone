using System.ComponentModel.DataAnnotations;

namespace CatalogService.Models;

public class Book
{
    [Key]
    public Guid BookId { get; set; } = Guid.NewGuid();

    [Required, MaxLength(20)]
    public string Isbn { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Author { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Genre { get; set; } = string.Empty;

    public int PublicationYear { get; set; }

    public string Description { get; set; } = string.Empty;

    [MaxLength(255)]
    public string Publisher { get; set; } = string.Empty;

    public int PageCount { get; set; }

    [MaxLength(50)]
    public string Language { get; set; } = "English";

    public int TotalCopies { get; set; }

    public int AvailableCopies { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Not persisted — computed from AvailableCopies
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Status => AvailableCopies > 0 ? "AVAILABLE" : "CHECKED_OUT";
}
