using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.DTOs;
using ReservationService.Models;

namespace ReservationService.Controllers;

// Internal, service-to-service only. Not part of the external API contract.
[ApiController]
[Route("api/internal/users")]
public class InternalController : ControllerBase
{
    private readonly ReservationDbContext _db;

    public InternalController(ReservationDbContext db)
    {
        _db = db;
    }

    [HttpGet("{userId}/stats")]
    public async Task<IActionResult> GetStats(Guid userId)
    {
        var activeReservations = await _db.Reservations.CountAsync(r =>
            r.UserId == userId &&
            (r.Status == ReservationStatus.RESERVED || r.Status == ReservationStatus.CHECKED_OUT));

        var borrowingHistory = await _db.Reservations.CountAsync(r =>
            r.UserId == userId && r.Status == ReservationStatus.RETURNED);

        return Ok(new InternalStatsResponse
        {
            ActiveReservations = activeReservations,
            BorrowingHistory = borrowingHistory
        });
    }
}
