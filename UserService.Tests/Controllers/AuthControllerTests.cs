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

public class AuthControllerTests
{
    private static UserDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new UserDbContext(options);
    }

    [Fact]
    public async Task Register_WithValidData_Returns201WithUserData()
    {
        var db = CreateInMemoryDb();
        var tokenService = new Mock<ITokenService>();
        var controller = new AuthController(db, tokenService.Object);

        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "SecurePass123!",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "+1-555-0100"
        };

        var result = await controller.Register(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);

        var response = Assert.IsType<RegisterResponse>(objectResult.Value);
        Assert.Equal("test@example.com", response.Email);
        Assert.Equal("PATRON", response.Role);
        Assert.Equal("ACTIVE", response.MembershipStatus);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        var db = CreateInMemoryDb();
        db.Users.Add(new User { Email = "existing@example.com", PasswordHash = "hash", FirstName = "A", LastName = "B", PhoneNumber = "+1-555-0000" });
        await db.SaveChangesAsync();

        var tokenService = new Mock<ITokenService>();
        var controller = new AuthController(db, tokenService.Object);

        var request = new RegisterRequest
        {
            Email = "existing@example.com",
            Password = "SecurePass123!",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "+1-555-0100"
        };

        var result = await controller.Register(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("VALIDATION_ERROR", error.Error);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndUser()
    {
        var db = CreateInMemoryDb();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass123!");
        db.Users.Add(new User
        {
            Email = "test@example.com",
            PasswordHash = passwordHash,
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "+1-555-0100",
            Role = UserRole.PATRON
        });
        await db.SaveChangesAsync();

        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(t => t.GenerateToken(It.IsAny<User>())).Returns("fake-jwt-token");

        var controller = new AuthController(db, tokenService.Object);

        var request = new LoginRequest { Email = "test@example.com", Password = "SecurePass123!" };
        var result = await controller.Login(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(okResult.Value);
        Assert.Equal("fake-jwt-token", response.AccessToken);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(86400, response.ExpiresIn);
        Assert.Equal("test@example.com", response.User.Email);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var db = CreateInMemoryDb();
        db.Users.Add(new User
        {
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPass123!"),
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "+1-555-0100"
        });
        await db.SaveChangesAsync();

        var tokenService = new Mock<ITokenService>();
        var controller = new AuthController(db, tokenService.Object);

        var request = new LoginRequest { Email = "test@example.com", Password = "WrongPassword!" };
        var result = await controller.Login(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(unauthorized.Value);
        Assert.Equal("AUTHENTICATION_FAILED", error.Error);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_Returns401()
    {
        var db = CreateInMemoryDb();
        var tokenService = new Mock<ITokenService>();
        var controller = new AuthController(db, tokenService.Object);

        var request = new LoginRequest { Email = "nobody@example.com", Password = "SomePassword123!" };
        var result = await controller.Login(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }
}
