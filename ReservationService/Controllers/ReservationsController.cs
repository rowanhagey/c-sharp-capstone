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
[Route("api/reservations")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly ReservationDbContext _db;
    private readonly ICatalogServiceClient _catalogClient;
    private readonly IWaitlistCascadeService _cascadeService;

    private const int MaxActiveReservations = 5;
    private const decimal LateFeePerDay = 1.00m;

    public ReservationsController(
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

    private bool IsLibrarian()
    {
        var role = User.FindFirstValue("role") ?? User.FindFirstValue(ClaimTypes.Role);
        return string.Equals(role, "LIBRARIAN", StringComparison.OrdinalIgnoreCase);
    }

    [HttpPost]
    public async Task<IActionResult> Reserve([FromBody] CreateReservationRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new ErrorResponse { Error = "UNAUTHORIZED", Message = "Authentication required" });

        var activeCount = await _db.Reservations.CountAsync(r =>
            r.UserId == userId &&
            (r.Status == ReservationStatus.RESERVED || r.Status == ReservationStatus.CHECKED_OUT));

        if (activeCount >= MaxActiveReservations)
        {
            return BadRequest(new
            {
                error = "RESERVATION_LIMIT_EXCEEDED",
                message = "You have reached the maximum of 5 active reservations",
                currentReservations = activeCount
            });
        }

        var availability = await _catalogClient.GetAvailabilityAsync(request.BookId);
        if (availability == null || !availability.Exists)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "Book not found" });
        }

        if (availability.AvailableCopies <= 0)
        {
            return BadRequest(new
            {
                error = "BOOK_UNAVAILABLE",
                message = "No copies available for reservation",
                availableCopies = availability.AvailableCopies
            });
        }

        var adjusted = await _catalogClient.AdjustAvailabilityAsync(request.BookId, -1);
        if (!adjusted)
        {
            return StatusCode(500, new ErrorResponse { Error = "INTERNAL_SERVER_ERROR", Message = "Failed to update book availability" });
        }

        var now = DateTime.UtcNow;
        var reservation = new Reservation
        {
            UserId = userId.Value,
            BookId = request.BookId,
            BookTitle = availability.Title,
            BookAuthor = availability.Author,
            Status = ReservationStatus.RESERVED,
            ReservedAt = now,
            ExpiresAt = now.AddDays(7)
        };

        _db.Reservations.Add(reservation);
        await _db.SaveChangesAsync();

        return StatusCode(201, new ReservationCreatedResponse
        {
            ReservationId = reservation.ReservationId,
            BookId = reservation.BookId,
            UserId = reservation.UserId,
            BookTitle = reservation.BookTitle,
            Status = reservation.Status.ToString(),
            ReservedAt = reservation.ReservedAt,
            ExpiresAt = reservation.ExpiresAt
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveReservations()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new ErrorResponse { Error = "UNAUTHORIZED", Message = "Authentication required" });

        var now = DateTime.UtcNow;

        var reservations = await _db.Reservations
            .Where(r => r.UserId == userId &&
                (r.Status == ReservationStatus.RESERVED || r.Status == ReservationStatus.CHECKED_OUT))
            .ToListAsync();

        var items = reservations.Select(r => new ActiveReservationItem
        {
            ReservationId = r.ReservationId,
            BookId = r.BookId,
            BookTitle = r.BookTitle,
            BookAuthor = r.BookAuthor,
            Status = r.Status.ToString(),
            ReservedAt = r.Status == ReservationStatus.RESERVED ? r.ReservedAt : null,
            ExpiresAt = r.Status == ReservationStatus.RESERVED ? r.ExpiresAt : null,
            DaysUntilExpiry = r.Status == ReservationStatus.RESERVED
                ? (int)Math.Ceiling((r.ExpiresAt - now).TotalDays)
                : null,
            CheckedOutAt = r.Status == ReservationStatus.CHECKED_OUT ? r.CheckedOutAt : null,
            DueDate = r.Status == ReservationStatus.CHECKED_OUT ? r.DueDate : null,
            DaysUntilDue = r.Status == ReservationStatus.CHECKED_OUT && r.DueDate.HasValue
                ? (int)Math.Ceiling((r.DueDate.Value - now).TotalDays)
                : null
        }).ToList();

        return Ok(new ActiveReservationsResponse
        {
            Reservations = items,
            TotalActive = items.Count
        });
    }

    [HttpPost("{reservationId}/checkout")]
    public async Task<IActionResult> Checkout(Guid reservationId, [FromBody] CheckoutRequest request)
    {
        if (!IsLibrarian())
            return StatusCode(403, new ErrorResponse { Error = "FORBIDDEN", Message = "Only librarians can checkout books" });

        var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.ReservationId == reservationId);
        if (reservation == null)
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "Reservation not found" });

        if (reservation.Status != ReservationStatus.RESERVED)
        {
            return BadRequest(new
            {
                error = "INVALID_STATUS",
                message = "Can only checkout reservations with RESERVED status",
                currentStatus = reservation.Status.ToString()
            });
        }

        var now = DateTime.UtcNow;
        reservation.Status = ReservationStatus.CHECKED_OUT;
        reservation.CheckedOutAt = now;
        reservation.DueDate = now.AddDays(14);
        reservation.Notes = request.Notes;

        await _db.SaveChangesAsync();

        return Ok(new CheckoutResponse
        {
            ReservationId = reservation.ReservationId,
            Status = reservation.Status.ToString(),
            CheckedOutAt = reservation.CheckedOutAt.Value,
            DueDate = reservation.DueDate.Value,
            Message = $"Book checked out successfully. Due date: {reservation.DueDate.Value:MMMM d, yyyy}"
        });
    }

    [HttpPost("{reservationId}/return")]
    public async Task<IActionResult> Return(Guid reservationId, [FromBody] ReturnRequest request)
    {
        if (!IsLibrarian())
            return StatusCode(403, new ErrorResponse { Error = "FORBIDDEN", Message = "Only librarians can process returns" });

        var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.ReservationId == reservationId);
        if (reservation == null)
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "Reservation not found" });

        if (reservation.Status != ReservationStatus.CHECKED_OUT)
        {
            return BadRequest(new
            {
                error = "INVALID_STATUS",
                message = "Can only return books with CHECKED_OUT status",
                currentStatus = reservation.Status.ToString()
            });
        }

        var now = DateTime.UtcNow;
        reservation.Status = ReservationStatus.RETURNED;
        reservation.ReturnedAt = now;
        reservation.Notes = request.Notes;

        if (Enum.TryParse<BookCondition>(request.Condition, ignoreCase: true, out var condition))
            reservation.Condition = condition;

        int lateDays = 0;
        decimal lateFee = 0.00m;

        if (reservation.DueDate.HasValue && now > reservation.DueDate.Value)
        {
            lateDays = (int)Math.Ceiling((now - reservation.DueDate.Value).TotalDays);
            lateFee = lateDays * LateFeePerDay;
        }

        reservation.LateDays = lateDays;
        reservation.LateFee = lateFee;

        await _db.SaveChangesAsync();

        await _cascadeService.OfferOrReleaseAsync(reservation.BookId);

        var message = lateFee > 0
            ? $"Book returned. Late fee of ${lateFee:0.00} applied to account."
            : "Book returned successfully";

        return Ok(new ReturnResponse
        {
            ReservationId = reservation.ReservationId,
            ReturnedAt = reservation.ReturnedAt.Value,
            DueDate = reservation.DueDate,
            LateDays = lateDays,
            LateFee = lateFee,
            Message = message
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 0, [FromQuery] int size = 20)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized(new ErrorResponse { Error = "UNAUTHORIZED", Message = "Authentication required" });

        var query = _db.Reservations.Where(r => r.UserId == userId);

        var totalElements = await query.CountAsync();
        var totalPages = size > 0 ? (int)Math.Ceiling(totalElements / (double)size) : 0;

        var all = await query.ToListAsync();

        var ordered = all
            .OrderByDescending(r => r.ReturnedAt ?? r.ReservedAt)
            .Skip(page * size)
            .Take(size)
            .Select(r => new HistoryItem
            {
                ReservationId = r.ReservationId,
                BookTitle = r.BookTitle,
                BookAuthor = r.BookAuthor,
                ReservedAt = r.ReservedAt,
                CheckedOutAt = r.CheckedOutAt,
                ReturnedAt = r.ReturnedAt,
                DueDate = r.DueDate,
                Status = r.Status.ToString(),
                WasLate = r.ReturnedAt.HasValue && r.DueDate.HasValue && r.ReturnedAt.Value > r.DueDate.Value
            })
            .ToList();

        return Ok(new PagedResult<HistoryItem>
        {
            Content = ordered,
            Page = page,
            Size = size,
            TotalElements = totalElements,
            TotalPages = totalPages,
            Last = page >= totalPages - 1
        });
    }
}
