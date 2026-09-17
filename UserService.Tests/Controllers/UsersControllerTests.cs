using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using UserService.Controllers;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using UserService.Services;
using Xunit;

namespace UserService.Tests.Controllers;

public class UsersControllerTests
{
    private static UserDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new UserDbContext(options);
    }

    private static UsersController CreateControllerWithUser(UserDbContext db, IReservationServiceClient client, Guid userId)
    {
        var controller = new UsersController(db, client);
        var claims = new List<Claim> { new("userId", userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    [Fact]
    public async Task GetProfile_WithValidUser_ReturnsProfileWithStats()
    {
        var userId = Guid.NewGuid();
        var db = CreateInMemoryDb();
        db.Users.Add(new User
        {
            UserId = userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "+1-555-0100",
            Role = UserRole.PATRON,
            MembershipStatus = MembershipStatus.ACTIVE
        });
        await db.SaveChangesAsync();

        var clientMock = new Mock<IReservationServiceClient>();
        clientMock.Setup(c => c.GetStatsAsync(userId))
            .ReturnsAsync(new ReservationStats { ActiveReservations = 2, BorrowingHistory = 10 });

        var controller = CreateControllerWithUser(db, clientMock.Object, userId);

        var result = await controller.GetProfile();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var profile = Assert.IsType<ProfileResponse>(okResult.Value);
        Assert.Equal("test@example.com", profile.Email);
        Assert.Equal(2, profile.ActiveReservations);
        Assert.Equal(10, profile.BorrowingHistory);
    }

    [Fact]
    public async Task GetProfile_WhenReservationServiceUnreachable_ReturnsZeroStats()
    {
        var userId = Guid.NewGuid();
        var db = CreateInMemoryDb();
        db.Users.Add(new User
        {
            UserId = userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "+1-555-0100"
        });
        await db.SaveChangesAsync();

        var clientMock = new Mock<IReservationServiceClient>();
        clientMock.Setup(c => c.GetStatsAsync(userId))
            .ReturnsAsync(new ReservationStats()); // simulates the graceful fallback

        var controller = CreateControllerWithUser(db, clientMock.Object, userId);

        var result = await controller.GetProfile();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var profile = Assert.IsType<ProfileResponse>(okResult.Value);
        Assert.Equal(0, profile.ActiveReservations);
        Assert.Equal(0, profile.BorrowingHistory);
    }

    [Fact]
    public async Task GetProfile_WithNoUserIdClaim_ReturnsUnauthorized()
    {
        var db = CreateInMemoryDb();
        var clientMock = new Mock<IReservationServiceClient>();
        var controller = new UsersController(db, clientMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var result = await controller.GetProfile();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetProfile_UserNotInDatabase_ReturnsNotFound()
    {
        var db = CreateInMemoryDb();
        var clientMock = new Mock<IReservationServiceClient>();
        var controller = CreateControllerWithUser(db, clientMock.Object, Guid.NewGuid());

        var result = await controller.GetProfile();

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
