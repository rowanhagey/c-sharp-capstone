using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserDbContext _db;
    private readonly ITokenService _tokenService;

    public AuthController(UserDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage))
            });
        }

        var emailExists = await _db.Users.AnyAsync(u => u.Email == request.Email);
        if (emailExists)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = "Email already exists"
            });
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Role = UserRole.PATRON,
            MembershipStatus = MembershipStatus.ACTIVE
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return StatusCode(201, new RegisterResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role.ToString(),
            MembershipStatus = user.MembershipStatus.ToString(),
            CreatedAt = user.CreatedAt
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new ErrorResponse
            {
                Error = "AUTHENTICATION_FAILED",
                Message = "Invalid email or password"
            });
        }

        var token = _tokenService.GenerateToken(user);

        return Ok(new LoginResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = 86400,
            User = new LoginUserSummary
            {
                UserId = user.UserId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role.ToString()
            }
        });
    }
}
