using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ReservationService.Services;
using Xunit;

namespace ReservationService.Tests.Services;

public class CatalogServiceClientTests
{
    private static CatalogServiceClient CreateClient(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost:5002") };
        var logger = Mock.Of<ILogger<CatalogServiceClient>>();
        return new CatalogServiceClient(httpClient, logger);
    }

    [Fact]
    public async Task GetAvailabilityAsync_Success_ReturnsAvailability()
    {
        var json = "{\"bookId\":\"" + Guid.NewGuid() + "\",\"title\":\"Test\",\"author\":\"Author\",\"availableCopies\":2,\"exists\":true}";
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        var client = CreateClient(response);

        var result = await client.GetAvailabilityAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.True(result!.Exists);
        Assert.Equal(2, result.AvailableCopies);
    }

    [Fact]
    public async Task GetAvailabilityAsync_ErrorResponse_ReturnsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var client = CreateClient(response);

        var result = await client.GetAvailabilityAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task AdjustAvailabilityAsync_Success_ReturnsTrue()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        var client = CreateClient(response);

        var result = await client.AdjustAvailabilityAsync(Guid.NewGuid(), -1);

        Assert.True(result);
    }

    [Fact]
    public async Task AdjustAvailabilityAsync_ErrorResponse_ReturnsFalse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
        var client = CreateClient(response);

        var result = await client.AdjustAvailabilityAsync(Guid.NewGuid(), -1);

        Assert.False(result);
    }
}
