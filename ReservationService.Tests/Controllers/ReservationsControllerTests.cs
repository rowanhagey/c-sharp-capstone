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

public class ReservationsControllerTests
{
    private static ReservationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ReservationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReservationDbContext(options);
    }

    private static ReservationsController CreateController(
        ReservationDbContext db,
        ICatalogServiceClient catalogClient,
        IWaitlistCascadeService cascadeService,
        Guid userId,
        string role = "PATRON")
    {
        var controller = new ReservationsController(db, catalogClient, cascadeService);
        var claims = new List<Claim> { new("userId", userId.ToString()), new("role", role) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task Reserve_AvailableBook_CreatesReservationAndReturns201()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var catalogMock = new Mock<ICatalogServiceClient>();
        catalogMock.Setup(c => c.GetAvailabilityAsync(bookId))
            .ReturnsAsync(new BookAvailability { BookId = bookId, Title = "Test Book", Author = "Test Author", AvailableCopies = 2, Exists = true });
        catalogMock.Setup(c => c.AdjustAvailabilityAsync(bookId, -1)).ReturnsAsync(true);

        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, userId);

        var result = await controller.Reserve(new CreateReservationRequest { BookId = bookId });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        var response = Assert.IsType<ReservationCreatedResponse>(objectResult.Value);
        Assert.Equal("RESERVED", response.Status);
        Assert.Equal("Test Book", response.BookTitle);
    }

    [Fact]
    public async Task Reserve_BookUnavailable_ReturnsBadRequest()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var catalogMock = new Mock<ICatalogServiceClient>();
        catalogMock.Setup(c => c.GetAvailabilityAsync(bookId))
            .ReturnsAsync(new BookAvailability { BookId = bookId, AvailableCopies = 0, Exists = true });

        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, userId);

        var result = await controller.Reserve(new CreateReservationRequest { BookId = bookId });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Reserve_AtFiveActiveReservations_ReturnsBadRequestLimitExceeded()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            db.Reservations.Add(new Reservation
            {
                UserId = userId,
                BookId = Guid.NewGuid(),
                Status = ReservationStatus.RESERVED,
                ReservedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
        }
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, userId);

        var result = await controller.Reserve(new CreateReservationRequest { BookId = Guid.NewGuid() });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("RESERVATION_LIMIT_EXCEEDED", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task Reserve_BookNotFound_Returns404()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var catalogMock = new Mock<ICatalogServiceClient>();
        catalogMock.Setup(c => c.GetAvailabilityAsync(bookId))
            .ReturnsAsync(new BookAvailability { Exists = false });

        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, userId);

        var result = await controller.Reserve(new CreateReservationRequest { BookId = bookId });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetActiveReservations_ReturnsOnlyReservedAndCheckedOut()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();

        db.Reservations.Add(new Reservation { UserId = userId, BookId = Guid.NewGuid(), Status = ReservationStatus.RESERVED, ReservedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7), BookTitle = "Book A" });
        db.Reservations.Add(new Reservation { UserId = userId, BookId = Guid.NewGuid(), Status = ReservationStatus.RETURNED, ReservedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7), BookTitle = "Book B" });
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, userId);

        var result = await controller.GetActiveReservations();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ActiveReservationsResponse>(okResult.Value);
        Assert.Equal(1, response.TotalActive);
        Assert.Equal("Book A", response.Reservations[0].BookTitle);
    }

    [Fact]
    public async Task Checkout_AsLibrarian_UpdatesStatusAndDueDate()
    {
        var db = CreateInMemoryDb();
        var reservation = new Reservation { UserId = Guid.NewGuid(), BookId = Guid.NewGuid(), Status = ReservationStatus.RESERVED, ReservedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7) };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, Guid.NewGuid(), role: "LIBRARIAN");

        var result = await controller.Checkout(reservation.ReservationId, new CheckoutRequest { Notes = "Good" });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CheckoutResponse>(okResult.Value);
        Assert.Equal("CHECKED_OUT", response.Status);
    }

    [Fact]
    public async Task Checkout_AsPatron_Returns403()
    {
        var db = CreateInMemoryDb();
        var reservation = new Reservation { UserId = Guid.NewGuid(), BookId = Guid.NewGuid(), Status = ReservationStatus.RESERVED, ReservedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7) };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, Guid.NewGuid(), role: "PATRON");

        var result = await controller.Checkout(reservation.ReservationId, new CheckoutRequest());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public async Task Checkout_WrongStatus_ReturnsBadRequest()
    {
        var db = CreateInMemoryDb();
        var reservation = new Reservation { UserId = Guid.NewGuid(), BookId = Guid.NewGuid(), Status = ReservationStatus.CHECKED_OUT, ReservedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7) };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, Guid.NewGuid(), role: "LIBRARIAN");

        var result = await controller.Checkout(reservation.ReservationId, new CheckoutRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Return_OnTime_NoLateFee()
    {
        var db = CreateInMemoryDb();
        var bookId = Guid.NewGuid();
        var reservation = new Reservation
        {
            UserId = Guid.NewGuid(),
            BookId = bookId,
            Status = ReservationStatus.CHECKED_OUT,
            ReservedAt = DateTime.UtcNow.AddDays(-5),
            ExpiresAt = DateTime.UtcNow.AddDays(2),
            CheckedOutAt = DateTime.UtcNow.AddDays(-4),
            DueDate = DateTime.UtcNow.AddDays(10)
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        cascadeMock.Setup(c => c.OfferOrReleaseAsync(bookId)).ReturnsAsync(false);

        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, Guid.NewGuid(), role: "LIBRARIAN");

        var result = await controller.Return(reservation.ReservationId, new ReturnRequest { Condition = "GOOD" });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ReturnResponse>(okResult.Value);
        Assert.Equal(0, response.LateDays);
        Assert.Equal(0.00m, response.LateFee);
        cascadeMock.Verify(c => c.OfferOrReleaseAsync(bookId), Times.Once);
    }

    [Fact]
    public async Task Return_Late_CalculatesFeeCorrectly()
    {
        var db = CreateInMemoryDb();
        var bookId = Guid.NewGuid();
        var reservation = new Reservation
        {
            UserId = Guid.NewGuid(),
            BookId = bookId,
            Status = ReservationStatus.CHECKED_OUT,
            ReservedAt = DateTime.UtcNow.AddDays(-20),
            ExpiresAt = DateTime.UtcNow.AddDays(-13),
            CheckedOutAt = DateTime.UtcNow.AddDays(-19),
            DueDate = DateTime.UtcNow.AddHours(-36) // 1.5 days overdue, rounds up to 2 late days
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        cascadeMock.Setup(c => c.OfferOrReleaseAsync(bookId)).ReturnsAsync(false);

        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, Guid.NewGuid(), role: "LIBRARIAN");

        var result = await controller.Return(reservation.ReservationId, new ReturnRequest { Condition = "GOOD" });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ReturnResponse>(okResult.Value);
        Assert.Equal(2, response.LateDays);
        Assert.Equal(2.00m, response.LateFee);
    }

    [Fact]
    public async Task GetHistory_ReturnsAllStatusesOrderedByMostRecent()
    {
        var db = CreateInMemoryDb();
        var userId = Guid.NewGuid();

        db.Reservations.Add(new Reservation { UserId = userId, BookId = Guid.NewGuid(), Status = ReservationStatus.RETURNED, ReservedAt = DateTime.UtcNow.AddDays(-30), ExpiresAt = DateTime.UtcNow.AddDays(-23), ReturnedAt = DateTime.UtcNow.AddDays(-25), BookTitle = "Old Book" });
        db.Reservations.Add(new Reservation { UserId = userId, BookId = Guid.NewGuid(), Status = ReservationStatus.RETURNED, ReservedAt = DateTime.UtcNow.AddDays(-5), ExpiresAt = DateTime.UtcNow.AddDays(2), ReturnedAt = DateTime.UtcNow.AddDays(-1), BookTitle = "Recent Book" });
        await db.SaveChangesAsync();

        var catalogMock = new Mock<ICatalogServiceClient>();
        var cascadeMock = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(db, catalogMock.Object, cascadeMock.Object, userId);

        var result = await controller.GetHistory();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PagedResult<HistoryItem>>(okResult.Value);
        Assert.Equal(2, response.TotalElements);
        Assert.Equal("Recent Book", response.Content[0].BookTitle);
    }
}
