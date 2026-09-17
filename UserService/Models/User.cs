using System.ComponentModel.DataAnnotations;

namespace UserService.Models;

public enum UserRole
{
    PATRON,
    LIBRARIAN
}

public enum MembershipStatus
{
    ACTIVE,
    SUSPENDED,
    INACTIVE
}

public class User
{
    [Key]
    public Guid UserId { get; set; } = Guid.NewGuid();

    [Required, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.PATRON;

    public MembershipStatus MembershipStatus { get; set; } = MembershipStatus.ACTIVE;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
