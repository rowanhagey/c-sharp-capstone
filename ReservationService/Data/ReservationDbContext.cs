using Microsoft.EntityFrameworkCore;
using ReservationService.Models;

namespace ReservationService.Data;

public class ReservationDbContext : DbContext
{
    public ReservationDbContext(DbContextOptions<ReservationDbContext> options) : base(options) { }

    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasIndex(r => r.UserId);
            entity.HasIndex(r => r.BookId);
            entity.Property(r => r.Status).HasConversion<string>();
            entity.Property(r => r.Condition).HasConversion<string>();
            entity.Property(r => r.LateFee).HasColumnType("decimal(10,2)");
        });

        modelBuilder.Entity<WaitlistEntry>(entity =>
        {
            entity.HasIndex(w => w.UserId);
            entity.HasIndex(w => w.BookId);
            entity.Property(w => w.Status).HasConversion<string>();
        });
    }
}
