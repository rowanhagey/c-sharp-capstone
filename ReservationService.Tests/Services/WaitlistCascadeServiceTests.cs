using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ReservationService.Data;
using ReservationService.Models;
using ReservationService.Services;
using Xunit;

namespace ReservationService.Tests.Services;

public class WaitlistCascadeServiceTests
{
    private static ReservationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReservationDbContext(options);
    }

    [Fact]
    public async Task OfferOrRelease_NoWaitersExist_ReleasesToCatalog()
    {
        var db = CreateInMemoryDb();
        var bookId = Guid.NewGuid();

        var catalogMock = new Mock<ICatalogServiceClient>();
        catalogMock.Setup(c => c.AdjustAvailabilityAsync(bookId, 1)).ReturnsAsync(true);

        var logger = Mock.Of<ILogger<WaitlistCascadeService>>();
        var service = new WaitlistCascadeService(db, catalogMock.Object, logger);

        var result = await service.OfferOrReleaseAsync(bookId);

        Assert.False(result);
        catalogMock.Verify(c => c.AdjustAvailabilityAsync(bookId, 1), Times.Once);
    }

    [Fact]
    public async Task OfferOrRelease_EligibleWaiterExists_CreatesReservationAndNotifies()
    {
        var db = CreateInMemoryDb();
        var bookId = Guid.NewGuid();
        var waiterId = Guid.NewGuid();

        var entry = new WaitlistEntry
        {
            UserId = waiterId,
            BookId = bookId,
            BookTitle = "Test Book",
            BookAuthor = "Test Author",
            Status = WaitlistStatus.WAITING,
            JoinedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var logger = Mock.Of<ILogger<WaitlistCascadeService>>();
        var service = new WaitlistCascadeService(db, catalogMock.Object, logger);

        var result = await service.OfferOrReleaseAsync(bookId);

        Assert.True(result);
        catalogMock.Verify(c => c.AdjustAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);

        var updatedEntry = await db.WaitlistEntries.FindAsync(entry.WaitlistId);
        Assert.Equal(WaitlistStatus.NOTIFIED, updatedEntry!.Status);
        Assert.NotNull(updatedEntry.ClaimDeadline);

        var newReservation = await db.Reservations.FirstOrDefaultAsync(r => r.UserId == waiterId && r.BookId == bookId);
        Assert.NotNull(newReservation);
        Assert.Equal(ReservationStatus.RESERVED, newReservation!.Status);
    }

    [Fact]
    public async Task OfferOrRelease_FirstWaiterAtLimit_SkipsToNextEligibleWaiter()
    {
        var db = CreateInMemoryDb();
        var bookId = Guid.NewGuid();
        var overLimitUserId = Guid.NewGuid();
        var eligibleUserId = Guid.NewGuid();

        // First-in-line user is already at the 5-reservation cap.
        for (int i = 0; i < 5; i++)
        {
            db.Reservations.Add(new Reservation
            {
                UserId = overLimitUserId,
                BookId = Guid.NewGuid(),
                Status = ReservationStatus.RESERVED,
                ReservedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
        }

        db.WaitlistEntries.Add(new WaitlistEntry { UserId = overLimitUserId, BookId = bookId, Status = WaitlistStatus.WAITING, JoinedAt = DateTime.UtcNow.AddMinutes(-20) });
        db.WaitlistEntries.Add(new WaitlistEntry { UserId = eligibleUserId, BookId = bookId, Status = WaitlistStatus.WAITING, JoinedAt = DateTime.UtcNow.AddMinutes(-10) });
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var logger = Mock.Of<ILogger<WaitlistCascadeService>>();
        var service = new WaitlistCascadeService(db, catalogMock.Object, logger);

        var result = await service.OfferOrReleaseAsync(bookId);

        Assert.True(result);

        // The over-limit user should NOT have gotten the new reservation.
        var overLimitReservation = await db.Reservations.FirstOrDefaultAsync(r => r.UserId == overLimitUserId && r.BookId == bookId);
        Assert.Null(overLimitReservation);

        // The eligible user should have.
        var eligibleReservation = await db.Reservations.FirstOrDefaultAsync(r => r.UserId == eligibleUserId && r.BookId == bookId);
        Assert.NotNull(eligibleReservation);

        // The skipped entry stays WAITING (still in queue for a future offer).
        var overLimitEntry = await db.WaitlistEntries.FirstAsync(w => w.UserId == overLimitUserId);
        Assert.Equal(WaitlistStatus.WAITING, overLimitEntry.Status);

        var eligibleEntry = await db.WaitlistEntries.FirstAsync(w => w.UserId == eligibleUserId);
        Assert.Equal(WaitlistStatus.NOTIFIED, eligibleEntry.Status);
    }

    [Fact]
    public async Task OfferOrRelease_AllWaitersAtLimit_ReleasesToCatalog()
    {
        var db = CreateInMemoryDb();
        var bookId = Guid.NewGuid();
        var overLimitUserId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            db.Reservations.Add(new Reservation
            {
                UserId = overLimitUserId,
                BookId = Guid.NewGuid(),
                Status = ReservationStatus.RESERVED,
                ReservedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
        }
        db.WaitlistEntries.Add(new WaitlistEntry { UserId = overLimitUserId, BookId = bookId, Status = WaitlistStatus.WAITING, JoinedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        catalogMock.Setup(c => c.AdjustAvailabilityAsync(bookId, 1)).ReturnsAsync(true);

        var logger = Mock.Of<ILogger<WaitlistCascadeService>>();
        var service = new WaitlistCascadeService(db, catalogMock.Object, logger);

        var result = await service.OfferOrReleaseAsync(bookId);

        Assert.False(result);
        catalogMock.Verify(c => c.AdjustAvailabilityAsync(bookId, 1), Times.Once);
    }
}
