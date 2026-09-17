using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using UserService.Models;
using UserService.Services;
using Xunit;

namespace UserService.Tests.Services;

public class TokenServiceTests
{
    private static IConfiguration CreateTestConfig()
    {
        var settings = new Dictionary<string, string?>
        {
            { "Jwt:Secret", "test-secret-key-that-is-long-enough-for-hmac-sha256" },
            { "Jwt:Issuer", "TestIssuer" },
            { "Jwt:Audience", "TestAudience" }
        };
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    [Fact]
    public void GenerateToken_ProducesValidJwtWithExpectedClaims()
    {
        var config = CreateTestConfig();
        var tokenService = new TokenService(config);

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Email = "test@example.com",
            Role = UserRole.LIBRARIAN
        };

        var token = tokenService.GenerateToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains(jwt.Claims, c => c.Type == "userId" && c.Value == user.UserId.ToString());
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "LIBRARIAN");
    }

    [Fact]
    public void GenerateToken_SetsExpirationApproximately24HoursOut()
    {
        var config = CreateTestConfig();
        var tokenService = new TokenService(config);
        var user = new User { UserId = Guid.NewGuid(), Email = "test@example.com", Role = UserRole.PATRON };

        var token = tokenService.GenerateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var expiresIn = jwt.ValidTo - DateTime.UtcNow;
        Assert.InRange(expiresIn.TotalHours, 23.9, 24.1);
    }
}
