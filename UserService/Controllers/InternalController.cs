using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.DTOs;

namespace UserService.Controllers;

// Internal, service-to-service only. Not part of the external API contract.
[ApiController]
[Route("api/internal/users")]
public class InternalController : ControllerBase
{
    private readonly UserDbContext _db;

    public InternalController(UserDbContext db)
    {
        _db = db;
    }

    [HttpGet("{userId}/validate")]
    public async Task<IActionResult> Validate(Guid userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
        {
            return Ok(new InternalUserValidationResponse { Exists = false });
        }

        return Ok(new InternalUserValidationResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            Role = user.Role.ToString(),
            MembershipStatus = user.MembershipStatus.ToString(),
            Exists = true
        });
    }
}
