using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models;

namespace ReservationService.Services;

public interface IWaitlistCascadeService
{
    Task<bool> OfferOrReleaseAsync(Guid bookId);
}

public class WaitlistCascadeService : IWaitlistCascadeService
{
    private readonly ReservationDbContext _db;
    private readonly ICatalogServiceClient _catalogClient;
    private readonly ILogger<WaitlistCascadeService> _logger;

    private const int MaxActiveReservations = 5;

    public WaitlistCascadeService(
        ReservationDbContext db,
        ICatalogServiceClient catalogClient,
        ILogger<WaitlistCascadeService> logger)
    {
        _db = db;
        _catalogClient = catalogClient;
        _logger = logger;
    }

    public async Task<bool> OfferOrReleaseAsync(Guid bookId)
    {
        var candidates = await _db.WaitlistEntries
            .Where(w => w.BookId == bookId && w.Status == WaitlistStatus.WAITING)
            .OrderBy(w => w.JoinedAt)
            .ToListAsync();

        foreach (var candidate in candidates)
        {
            var activeCount = await _db.Reservations.CountAsync(r =>
                r.UserId == candidate.UserId &&
                (r.Status == ReservationStatus.RESERVED || r.Status == ReservationStatus.CHECKED_OUT));

            if (activeCount >= MaxActiveReservations)
            {
                continue;
            }

            var now = DateTime.UtcNow;

            var reservation = new Reservation
            {
                UserId = candidate.UserId,
                BookId = candidate.BookId,
                BookTitle = candidate.BookTitle,
                BookAuthor = candidate.BookAuthor,
                Status = ReservationStatus.RESERVED,
                ReservedAt = now,
                ExpiresAt = now.AddDays(7)
            };
            _db.Reservations.Add(reservation);

            candidate.Status = WaitlistStatus.NOTIFIED;
            candidate.NotifiedAt = now;
            candidate.ClaimDeadline = now.AddHours(48);

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Book {BookId} offered to waitlist user {UserId} (waitlist {WaitlistId}), new reservation {ReservationId}",
                bookId, candidate.UserId, candidate.WaitlistId, reservation.ReservationId);

            return true;
        }

        await _catalogClient.AdjustAvailabilityAsync(bookId, +1);
        return false;
    }
}
