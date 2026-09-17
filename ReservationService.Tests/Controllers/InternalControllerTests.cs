using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Controllers;
using ReservationService.Data;
using ReservationService.DTOs;
using ReservationService.Models;
using Xunit;

namespace ReservationService.Tests.Controllers;

public class InternalControllerTests
{
    private static ReservationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReservationDbContext(options);
    }

    [Fact]
    public async Task GetStats_CountsActiveAndReturnedCorrectly()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();

        db.Reservations.Add(new Reservation { UserId = userId, BookId = Guid.NewGuid(), Status = ReservationStatus.RESERVED, ReservedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7) });
        db.Reservations.Add(new Reservation { UserId = userId, BookId = Guid.NewGuid(), Status = ReservationStatus.CHECKED_OUT, ReservedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7) });
        db.Reservations.Add(new Reservation { UserId = userId, BookId = Guid.NewGuid(), Status = ReservationStatus.RETURNED, ReservedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7) });
        db.Reservations.Add(new Reservation { UserId = userId, BookId = Guid.NewGuid(), Status = ReservationStatus.RETURNED, ReservedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7) });
        await db.SaveChangesAsync();

        var controller = new InternalController(db);
        var result = await controller.GetStats(userId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InternalStatsResponse>(okResult.Value);
        Assert.Equal(2, response.ActiveReservations);
        Assert.Equal(2, response.BorrowingHistory);
    }
}
