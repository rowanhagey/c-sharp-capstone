using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ReservationService.Data;
using ReservationService.Models;
using ReservationService.Services;
using Xunit;

namespace ReservationService.Tests.Services;

public class WaitlistExpiryJobTests
{
    [Fact]
    public async Task ExecuteAsync_ExpiredNotifiedEntry_CancelsReservationAndCascades()
    {
        var dbName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ReservationDbContext>(options => options.UseInMemoryDatabase(dbName));

        var catalogMock = new Mock<ICatalogServiceClient>();
        catalogMock.Setup(c => c.AdjustAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<int>())).ReturnsAsync(true);
        services.AddSingleton(catalogMock.Object);
        services.AddScoped<IWaitlistCascadeService, WaitlistCascadeService>();

        var provider = services.BuildServiceProvider();

        // Seed an expired NOTIFIED entry with its holding reservation.
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        Guid waitlistId;
        Guid reservationId;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();

            var entry = new WaitlistEntry
            {
                UserId = userId,
                BookId = bookId,
                Status = WaitlistStatus.NOTIFIED,
                JoinedAt = DateTime.UtcNow.AddDays(-3),
                NotifiedAt = DateTime.UtcNow.AddHours(-50),
                ClaimDeadline = DateTime.UtcNow.AddHours(-2) // deadline already passed
            };
            db.WaitlistEntries.Add(entry);

            var reservation = new Reservation
            {
                UserId = userId,
                BookId = bookId,
                Status = ReservationStatus.RESERVED,
                ReservedAt = DateTime.UtcNow.AddHours(-50),
                ExpiresAt = DateTime.UtcNow.AddDays(4)
            };
            db.Reservations.Add(reservation);

            await db.SaveChangesAsync();
            waitlistId = entry.WaitlistId;
            reservationId = reservation.ReservationId;
        }

        var logger = Mock.Of<ILogger<WaitlistExpiryJob>>();
        var job = new WaitlistExpiryJob(provider, logger);

        using var cts = new CancellationTokenSource();
        await job.StartAsync(cts.Token);

        // Poll for up to 5 seconds for the background cycle to complete, instead of a fixed sleep.
        WaitlistEntry? polledEntry = null;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            using (var pollScope = provider.CreateScope())
            {
                var pollDb = pollScope.ServiceProvider.GetRequiredService<ReservationDbContext>();
                polledEntry = await pollDb.WaitlistEntries.FindAsync(waitlistId);
                if (polledEntry!.Status == WaitlistStatus.EXPIRED) break;
            }
            await Task.Delay(100);
        }

        cts.Cancel();
        await job.StopAsync(CancellationToken.None);

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();

            var finalEntry = await db.WaitlistEntries.FindAsync(waitlistId);
            Assert.Equal(WaitlistStatus.EXPIRED, finalEntry!.Status);

            var reservation = await db.Reservations.FindAsync(reservationId);
            Assert.Equal(ReservationStatus.CANCELLED, reservation!.Status);
        }

        // No one else was waiting, so the cascade should have released the copy back to Catalog.
        catalogMock.Verify(c => c.AdjustAvailabilityAsync(bookId, 1), Times.Once);
    }
}
