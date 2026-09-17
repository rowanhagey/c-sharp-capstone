using System.ComponentModel.DataAnnotations;

namespace ReservationService.Models;

public enum ReservationStatus
{
    RESERVED,
    CHECKED_OUT,
    RETURNED,
    CANCELLED
}

public enum BookCondition
{
    GOOD,
    FAIR,
    POOR,
    DAMAGED
}

public class Reservation
{
    [Key]
    public Guid ReservationId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid BookId { get; set; }

    // Denormalized snapshot for display without a live call every time — refreshed from Catalog on write.
    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;

    public ReservationStatus Status { get; set; } = ReservationStatus.RESERVED;

    public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public DateTime? CheckedOutAt { get; set; }
    public DateTime? DueDate { get; set; }

    public DateTime? ReturnedAt { get; set; }
    public BookCondition? Condition { get; set; }
    public int? LateDays { get; set; }
    public decimal? LateFee { get; set; }

    public string? Notes { get; set; }
}
