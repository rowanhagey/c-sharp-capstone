using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Controllers;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using Xunit;

namespace UserService.Tests.Controllers;

public class InternalControllerTests
{
    private static UserDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new UserDbContext(options);
    }

    [Fact]
    public async Task Validate_ExistingUser_ReturnsUserDetails()
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
            Role = UserRole.LIBRARIAN,
            MembershipStatus = MembershipStatus.ACTIVE
        });
        await db.SaveChangesAsync();

        var controller = new InternalController(db);
        var result = await controller.Validate(userId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InternalUserValidationResponse>(okResult.Value);
        Assert.True(response.Exists);
        Assert.Equal("test@example.com", response.Email);
        Assert.Equal("LIBRARIAN", response.Role);
    }

    [Fact]
    public async Task Validate_NonExistentUser_ReturnsExistsFalse()
    {
        var db = CreateInMemoryDb();
        var controller = new InternalController(db);

        var result = await controller.Validate(Guid.NewGuid());

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<InternalUserValidationResponse>(okResult.Value);
        Assert.False(response.Exists);
    }
}
