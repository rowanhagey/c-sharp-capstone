namespace ReservationService.Services;

public class BookAvailability
{
    public Guid BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int AvailableCopies { get; set; }
    public bool Exists { get; set; }
}

public interface ICatalogServiceClient
{
    Task<BookAvailability?> GetAvailabilityAsync(Guid bookId);
    Task<bool> AdjustAvailabilityAsync(Guid bookId, int delta);
}

public class CatalogServiceClient : ICatalogServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogServiceClient> _logger;

    public CatalogServiceClient(HttpClient httpClient, ILogger<CatalogServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BookAvailability?> GetAvailabilityAsync(Guid bookId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/internal/books/{bookId}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<BookAvailability>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch availability for book {BookId}", bookId);
            return null;
        }
    }

    public async Task<bool> AdjustAvailabilityAsync(Guid bookId, int delta)
    {
        try
        {
            var response = await _httpClient.PatchAsJsonAsync(
                $"/api/internal/books/{bookId}/availability",
                new { delta });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to adjust availability for book {BookId}", bookId);
            return false;
        }
    }
}
