namespace UserService.Services;

public class ReservationStats
{
    public int ActiveReservations { get; set; }
    public int BorrowingHistory { get; set; }
}

public interface IReservationServiceClient
{
    Task<ReservationStats> GetStatsAsync(Guid userId);
}

// Calls an internal endpoint on Reservation Service.
// If Reservation Service is unreachable, returns zeros rather than failing the profile request.
public class ReservationServiceClient : IReservationServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReservationServiceClient> _logger;

    public ReservationServiceClient(HttpClient httpClient, ILogger<ReservationServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ReservationStats> GetStatsAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/internal/users/{userId}/stats");
            if (!response.IsSuccessStatusCode)
            {
                return new ReservationStats();
            }
            var stats = await response.Content.ReadFromJsonAsync<ReservationStats>();
            return stats ?? new ReservationStats();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch reservation stats for user {UserId}", userId);
            return new ReservationStats();
        }
    }
}
