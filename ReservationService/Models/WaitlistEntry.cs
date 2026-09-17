using System.ComponentModel.DataAnnotations;

namespace ReservationService.Models;

public enum WaitlistStatus
{
    WAITING,
    NOTIFIED,
    CLAIMED,
    CANCELLED,
    EXPIRED
}

public class WaitlistEntry
{
    [Key]
    public Guid WaitlistId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid BookId { get; set; }

    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;

    public WaitlistStatus Status { get; set; } = WaitlistStatus.WAITING;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public DateTime? NotifiedAt { get; set; }
    public DateTime? ClaimDeadline { get; set; }
}
