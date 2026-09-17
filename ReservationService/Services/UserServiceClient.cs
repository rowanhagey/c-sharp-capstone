namespace ReservationService.Services;

public class UserValidation
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string MembershipStatus { get; set; } = string.Empty;
    public bool Exists { get; set; }
}

public interface IUserServiceClient
{
    Task<UserValidation?> ValidateUserAsync(Guid userId);
}

public class UserServiceClient : IUserServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserServiceClient> _logger;

    public UserServiceClient(HttpClient httpClient, ILogger<UserServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UserValidation?> ValidateUserAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/internal/users/{userId}/validate");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<UserValidation>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate user {UserId}", userId);
            return null;
        }
    }
}
