using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ReservationService.Services;
using Xunit;

namespace ReservationService.Tests.Services;

public class UserServiceClientTests
{
    [Fact]
    public async Task ValidateUserAsync_Success_ReturnsValidation()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var json = "{\"userId\":\"" + Guid.NewGuid() + "\",\"email\":\"test@example.com\",\"role\":\"PATRON\",\"membershipStatus\":\"ACTIVE\",\"exists\":true}";
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost:5001") };
        var client = new UserServiceClient(httpClient, Mock.Of<ILogger<UserServiceClient>>());

        var result = await client.ValidateUserAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.True(result!.Exists);
        Assert.Equal("PATRON", result.Role);
    }

    [Fact]
    public async Task ValidateUserAsync_NetworkFailure_ReturnsNullGracefully()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost:5001") };
        var client = new UserServiceClient(httpClient, Mock.Of<ILogger<UserServiceClient>>());

        var result = await client.ValidateUserAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
