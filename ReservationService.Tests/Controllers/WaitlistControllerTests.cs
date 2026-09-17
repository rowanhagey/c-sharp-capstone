using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using ReservationService.Controllers;
using ReservationService.Data;
using ReservationService.DTOs;
using ReservationService.Models;
using ReservationService.Services;
using Xunit;

namespace ReservationService.Tests.Controllers;

public class WaitlistControllerTests
{
    private static ReservationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReservationDbContext(options);
    }

    private static WaitlistController CreateController(
        ReservationDbContext db,
        ICatalogServiceClient catalogClient,
        IWaitlistCascadeService cascadeService,
        Guid userId)
    {
        var controller = new WaitlistController(db, catalogClient, cascadeService);
        var claims = new List<Claim> { new("userId", userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task JoinWaitlist_BookUnavailable_CreatesEntryWithPosition1()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var catalogMock = new Mock<ICatalogServiceClient>();
        catalogMock.Setup(c => c.GetAvailabilityAsync(bookId))
            .ReturnsAsync(new BookAvailability { BookId = bookId, Title = "Test Book", Author = "Test Author", AvailableCopies = 0, Exists = true });

        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, userId);

        var result = await controller.JoinWaitlist(new JoinWaitlistRequest { BookId = bookId });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        var response = Assert.IsType<WaitlistCreatedResponse>(objectResult.Value);
        Assert.Equal(1, response.Position);
        Assert.Equal("WAITING", response.Status);
    }

    [Fact]
    public async Task JoinWaitlist_BookHasAvailableCopies_ReturnsBadRequestBookAvailable()
    {
        var db = CreateInMemoryDb();
        var bookId = Guid.NewGuid();

        var catalogMock = new Mock<ICatalogServiceClient>();
        catalogMock.Setup(c => c.GetAvailabilityAsync(bookId))
            .ReturnsAsync(new BookAvailability { AvailableCopies = 3, Exists = true });

        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, Guid.NewGuid());

        var result = await controller.JoinWaitlist(new JoinWaitlistRequest { BookId = bookId });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("BOOK_AVAILABLE", error.Error);
    }

    [Fact]
    public async Task JoinWaitlist_AlreadyWaiting_ReturnsBadRequestAlreadyWaitlisted()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        db.WaitlistEntries.Add(new WaitlistEntry { UserId = userId, BookId = bookId, Status = WaitlistStatus.WAITING, JoinedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        catalogMock.Setup(c => c.GetAvailabilityAsync(bookId))
            .ReturnsAsync(new BookAvailability { AvailableCopies = 0, Exists = true });

        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, userId);

        var result = await controller.JoinWaitlist(new JoinWaitlistRequest { BookId = bookId });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("ALREADY_WAITLISTED", error.Error);
    }

    [Fact]
    public async Task GetMyWaitlist_WaitingEntry_ComputesPosition()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        db.WaitlistEntries.Add(new WaitlistEntry { UserId = otherUserId, BookId = bookId, Status = WaitlistStatus.WAITING, JoinedAt = DateTime.UtcNow.AddMinutes(-10) });
        db.WaitlistEntries.Add(new WaitlistEntry { UserId = userId, BookId = bookId, Status = WaitlistStatus.WAITING, JoinedAt = DateTime.UtcNow.AddMinutes(-5) });
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, userId);

        var result = await controller.GetMyWaitlist();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<WaitlistEntriesResponse>(okResult.Value);
        Assert.Single(response.Entries);
        Assert.Equal(2, response.Entries[0].Position);
    }

    [Fact]
    public async Task GetMyWaitlist_NotifiedEntry_ShowsClaimDeadlineNotPosition()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var claimDeadline = DateTime.UtcNow.AddHours(48);

        db.WaitlistEntries.Add(new WaitlistEntry
        {
            UserId = userId,
            BookId = Guid.NewGuid(),
            Status = WaitlistStatus.NOTIFIED,
            JoinedAt = DateTime.UtcNow.AddDays(-1),
            NotifiedAt = DateTime.UtcNow,
            ClaimDeadline = claimDeadline
        });
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, userId);

        var result = await controller.GetMyWaitlist();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<WaitlistEntriesResponse>(okResult.Value);
        Assert.Null(response.Entries[0].Position);
        Assert.Equal(claimDeadline, response.Entries[0].ClaimDeadline);
    }

    [Fact]
    public async Task LeaveWaitlist_WaitingEntry_CancelsWithoutCascade()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var entry = new WaitlistEntry { UserId = userId, BookId = Guid.NewGuid(), Status = WaitlistStatus.WAITING, JoinedAt = DateTime.UtcNow };
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, userId);

        var result = await controller.LeaveWaitlist(entry.WaitlistId);

        Assert.IsType<OkObjectResult>(result);
        cascadeMock.Verify(c => c.OfferOrReleaseAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task LeaveWaitlist_NotifiedEntry_CancelsReservationAndTriggersCascade()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var entry = new WaitlistEntry { UserId = userId, BookId = bookId, Status = WaitlistStatus.NOTIFIED, JoinedAt = DateTime.UtcNow.AddDays(-1), NotifiedAt = DateTime.UtcNow, ClaimDeadline = DateTime.UtcNow.AddHours(48) };
        db.WaitlistEntries.Add(entry);

        var reservation = new Reservation { UserId = userId, BookId = bookId, Status = ReservationStatus.RESERVED, ReservedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7) };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        cascadeMock.Setup(c => c.OfferOrReleaseAsync(bookId)).ReturnsAsync(false);

        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, userId);

        var result = await controller.LeaveWaitlist(entry.WaitlistId);

        Assert.IsType<OkObjectResult>(result);
        cascadeMock.Verify(c => c.OfferOrReleaseAsync(bookId), Times.Once);

        var updatedReservation = await db.Reservations.FindAsync(reservation.ReservationId);
        Assert.Equal(ReservationStatus.CANCELLED, updatedReservation!.Status);
    }

    [Fact]
    public async Task LeaveWaitlist_NotOwnedByUser_Returns404()
    {
        var db = CreateInMemoryDb();
        var entry = new WaitlistEntry { UserId = Guid.NewGuid(), BookId = Guid.NewGuid(), Status = WaitlistStatus.WAITING, JoinedAt = DateTime.UtcNow };
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, Guid.NewGuid()); // different user

        var result = await controller.LeaveWaitlist(entry.WaitlistId);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
