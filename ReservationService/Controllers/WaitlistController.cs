using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.DTOs;
using ReservationService.Models;
using ReservationService.Services;

namespace ReservationService.Controllers;

[ApiController]
[Route("api/reservations/waitlist")]
[Authorize]
public class WaitlistController : ControllerBase
{
    private readonly ReservationDbContext _db;
    private readonly ICatalogServiceClient _catalogClient;
    private readonly IWaitlistCascadeService _cascadeService;

    public WaitlistController(
        ReservationDbContext db,
        ICatalogServiceClient catalogClient,
        IWaitlistCascadeService cascadeService)
    {
        _db = db;
        _catalogClient = catalogClient;
        _cascadeService = cascadeService;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue("userId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    [HttpPost]
    public async Task<IActionResult> JoinWaitlist([FromBody] JoinWaitlistRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new ErrorResponse { Error = "UNAUTHORIZED", Message = "Authentication required" });

        var availability = await _catalogClient.GetAvailabilityAsync(request.BookId);
        if (availability == null || !availability.Exists)
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "Book not found" });

        if (availability.AvailableCopies > 0)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "BOOK_AVAILABLE",
                Message = "This book currently has available copies - reserve it directly instead of joining the waitlist"
            });
        }

        var alreadyWaiting = await _db.WaitlistEntries.AnyAsync(w =>
            w.UserId == userId && w.BookId == request.BookId && w.Status == WaitlistStatus.WAITING);

        if (alreadyWaiting)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "ALREADY_WAITLISTED",
                Message = "You are already on the waitlist for this book"
            });
        }

        var now = DateTime.UtcNow;
        var entry = new WaitlistEntry
        {
            UserId = userId.Value,
            BookId = request.BookId,
            BookTitle = availability.Title,
            BookAuthor = availability.Author,
            Status = WaitlistStatus.WAITING,
            JoinedAt = now
        };

        _db.WaitlistEntries.Add(entry);
        await _db.SaveChangesAsync();

        var position = await _db.WaitlistEntries
            .Where(w => w.BookId == request.BookId && w.Status == WaitlistStatus.WAITING)
            .OrderBy(w => w.JoinedAt)
            .Select(w => w.WaitlistId)
            .ToListAsync();

        var rank = position.IndexOf(entry.WaitlistId) + 1;

        return StatusCode(201, new WaitlistCreatedResponse
        {
            WaitlistId = entry.WaitlistId,
            BookId = entry.BookId,
            BookTitle = entry.BookTitle,
            Status = entry.Status.ToString(),
            JoinedAt = entry.JoinedAt,
            Position = rank
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyWaitlist()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new ErrorResponse { Error = "UNAUTHORIZED", Message = "Authentication required" });

        var myEntries = await _db.WaitlistEntries
            .Where(w => w.UserId == userId && (w.Status == WaitlistStatus.WAITING || w.Status == WaitlistStatus.NOTIFIED))
            .ToListAsync();

        var items = new List<WaitlistEntryItem>();

        foreach (var entry in myEntries)
        {
            int? position = null;

            if (entry.Status == WaitlistStatus.WAITING)
            {
                var queue = await _db.WaitlistEntries
                    .Where(w => w.BookId == entry.BookId && w.Status == WaitlistStatus.WAITING)
                    .OrderBy(w => w.JoinedAt)
                    .Select(w => w.WaitlistId)
                    .ToListAsync();

                position = queue.IndexOf(entry.WaitlistId) + 1;
            }

            items.Add(new WaitlistEntryItem
            {
                WaitlistId = entry.WaitlistId,
                BookId = entry.BookId,
                BookTitle = entry.BookTitle,
                BookAuthor = entry.BookAuthor,
                Status = entry.Status.ToString(),
                JoinedAt = entry.JoinedAt,
                Position = position,
                NotifiedAt = entry.NotifiedAt,
                ClaimDeadline = entry.ClaimDeadline
            });
        }

        return Ok(new WaitlistEntriesResponse { Entries = items });
    }

    [HttpDelete("{waitlistId}")]
    public async Task<IActionResult> LeaveWaitlist(Guid waitlistId)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new ErrorResponse { Error = "UNAUTHORIZED", Message = "Authentication required" });

        var entry = await _db.WaitlistEntries.FirstOrDefaultAsync(w => w.WaitlistId == waitlistId && w.UserId == userId);
        if (entry == null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "Waitlist entry not found" });
        }

        var wasNotified = entry.Status == WaitlistStatus.NOTIFIED;
        var bookId = entry.BookId;

        entry.Status = WaitlistStatus.CANCELLED;

        if (wasNotified)
        {
            // This entry was actively holding a claim on a reserved copy — cancel that reservation too.
            var reservation = await _db.Reservations
                .Where(r => r.UserId == userId && r.BookId == bookId && r.Status == ReservationStatus.RESERVED)
                .OrderByDescending(r => r.ReservedAt)
                .FirstOrDefaultAsync();

            if (reservation != null)
            {
                reservation.Status = ReservationStatus.CANCELLED;
            }
        }

        await _db.SaveChangesAsync();

        if (wasNotified)
        {
            await _cascadeService.OfferOrReleaseAsync(bookId);
        }

        return Ok(new WaitlistCancelResponse { WaitlistId = entry.WaitlistId });
    }
}
