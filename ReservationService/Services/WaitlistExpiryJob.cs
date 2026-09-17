using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models;

namespace ReservationService.Services;

public class WaitlistExpiryJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WaitlistExpiryJob> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    public WaitlistExpiryJob(IServiceProvider serviceProvider, ILogger<WaitlistExpiryJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireOverdueClaimsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Waitlist expiry job failed on this cycle");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task ExpireOverdueClaimsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var cascade = scope.ServiceProvider.GetRequiredService<IWaitlistCascadeService>();

        var now = DateTime.UtcNow;

        var expired = await db.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.NOTIFIED && w.ClaimDeadline != null && w.ClaimDeadline < now)
            .ToListAsync(ct);

        foreach (var entry in expired)
        {
            var reservation = await db.Reservations
                .Where(r => r.UserId == entry.UserId && r.BookId == entry.BookId && r.Status == ReservationStatus.RESERVED)
                .OrderByDescending(r => r.ReservedAt)
                .FirstOrDefaultAsync(ct);

            if (reservation != null)
            {
                reservation.Status = ReservationStatus.CANCELLED;
            }

            entry.Status = WaitlistStatus.EXPIRED;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Waitlist entry {WaitlistId} expired, cascading book {BookId}", entry.WaitlistId, entry.BookId);
            await cascade.OfferOrReleaseAsync(entry.BookId);
        }
    }
}
