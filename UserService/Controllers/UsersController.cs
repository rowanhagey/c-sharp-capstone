using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.DTOs;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserDbContext _db;
    private readonly IReservationServiceClient _reservationClient;

    public UsersController(UserDbContext db, IReservationServiceClient reservationClient)
    {
        _db = db;
        _reservationClient = reservationClient;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userIdClaim = User.FindFirstValue("userId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse { Error = "UNAUTHORIZED", Message = "Authentication required" });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "User not found" });
        }

        var stats = await _reservationClient.GetStatsAsync(userId);

        return Ok(new ProfileResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString(),
            MembershipStatus = user.MembershipStatus.ToString(),
            MemberSince = user.CreatedAt,
            ActiveReservations = stats.ActiveReservations,
            BorrowingHistory = stats.BorrowingHistory
        });
    }
}
