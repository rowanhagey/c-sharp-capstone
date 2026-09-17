using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using UserService.Services;
using Xunit;

namespace UserService.Tests.Services;

public class ReservationServiceClientTests
{
    private static ReservationServiceClient CreateClient(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost:5003") };
        var logger = new Mock<ILogger<ReservationServiceClient>>();

        return new ReservationServiceClient(httpClient, logger.Object);
    }

    [Fact]
    public async Task GetStatsAsync_SuccessfulResponse_ReturnsStats()
    {
        var json = "{\"activeReservations\":3,\"borrowingHistory\":12}";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        var client = CreateClient(response);
        var stats = await client.GetStatsAsync(Guid.NewGuid());

        Assert.Equal(3, stats.ActiveReservations);
        Assert.Equal(12, stats.BorrowingHistory);
    }

    [Fact]
    public async Task GetStatsAsync_ServiceReturnsError_ReturnsZeroStats()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var client = CreateClient(response);

        var stats = await client.GetStatsAsync(Guid.NewGuid());

        Assert.Equal(0, stats.ActiveReservations);
        Assert.Equal(0, stats.BorrowingHistory);
    }

    [Fact]
    public async Task GetStatsAsync_ThrowsException_ReturnsZeroStatsGracefully()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost:5003") };
        var logger = new Mock<ILogger<ReservationServiceClient>>();
        var client = new ReservationServiceClient(httpClient, logger.Object);

        var stats = await client.GetStatsAsync(Guid.NewGuid());

        Assert.Equal(0, stats.ActiveReservations);
        Assert.Equal(0, stats.BorrowingHistory);
    }
}
