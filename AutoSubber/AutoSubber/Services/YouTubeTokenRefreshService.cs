using AutoSubber.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoSubber.Services
{
    /// <summary>
    /// Implementation of YouTube token refresh service using Google OAuth2
    /// </summary>
    public class YouTubeTokenRefreshService : IYouTubeTokenRefreshService
    {
        private readonly ILogger<YouTubeTokenRefreshService> _logger;
        private readonly ITokenEncryptionService _tokenEncryption;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public YouTubeTokenRefreshService(
            ILogger<YouTubeTokenRefreshService> logger,
            ITokenEncryptionService tokenEncryption,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _logger = logger;
            _tokenEncryption = tokenEncryption;
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<bool> RefreshUserTokenAsync(ApplicationUser user)
        {
            try
            {
                if (string.IsNullOrEmpty(user.EncryptedRefreshToken))
                {
                    _logger.LogWarning("User {UserId} does not have a refresh token", user.Id);
                    return false;
                }

                var refreshToken = _tokenEncryption.Decrypt(user.EncryptedRefreshToken);
                if (string.IsNullOrEmpty(refreshToken))
                {
                    _logger.LogError("Failed to decrypt refresh token for user {UserId}", user.Id);
                    return false;
                }

                var clientId = _configuration["Authentication:Google:ClientId"];
                var clientSecret = _configuration["Authentication:Google:ClientSecret"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("Google OAuth credentials not configured");
                    return false;
                }

                // Prepare the refresh request
                var refreshRequest = new[]
                {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("refresh_token", refreshToken),
                    new KeyValuePair<string, string>("grant_type", "refresh_token")
                };

                var content = new FormUrlEncodedContent(refreshRequest);

                // Make the refresh request to Google
                var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Token refresh failed for user {UserId} with status {StatusCode}: {ErrorContent}", 
                        user.Id, response.StatusCode, errorContent);
                    return false;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(responseContent);

                if (tokenResponse?.AccessToken == null)
                {
                    _logger.LogError("Invalid token response received for user {UserId}", user.Id);
                    return false;
                }

                // Update the user's access token and expiry
                user.EncryptedAccessToken = _tokenEncryption.Encrypt(tokenResponse.AccessToken);
                
                // Calculate expiry time (Google returns expires_in as seconds)
                if (tokenResponse.ExpiresIn.HasValue)
                {
                    user.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn.Value);
                }

                // If a new refresh token was provided, update it
                if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
                {
                    user.EncryptedRefreshToken = _tokenEncryption.Encrypt(tokenResponse.RefreshToken);
                }

                // Re-enable automation if it was disabled
                user.AutomationDisabled = false;

                await _userManager.UpdateAsync(user);

                _logger.LogInformation("Successfully refreshed token for user {UserId}", user.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token for user {UserId}", user.Id);
                return false;
            }
        }

        public bool TokenNeedsRefresh(ApplicationUser user, int bufferMinutes = 30)
        {
            // If automation is disabled, don't attempt refresh
            if (user.AutomationDisabled)
            {
                return false;
            }

            // If no expiry time is set, consider it needing refresh
            if (!user.TokenExpiresAt.HasValue)
            {
                return !string.IsNullOrEmpty(user.EncryptedRefreshToken);
            }

            // Check if token expires within the buffer time
            var expiryThreshold = DateTime.UtcNow.AddMinutes(bufferMinutes);
            return user.TokenExpiresAt.Value <= expiryThreshold;
        }

        public async Task DisableAutomationAsync(ApplicationUser user)
        {
            try
            {
                user.AutomationDisabled = true;
                await _userManager.UpdateAsync(user);
                
                _logger.LogWarning("Automation disabled for user {UserId} due to token refresh failure. User will need to re-authenticate.", 
                    user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling automation for user {UserId}", user.Id);
            }
        }

        /// <summary>
        /// Data transfer object for Google's token refresh response
        /// </summary>
        private class GoogleTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }
            
            [JsonPropertyName("expires_in")]
            public int? ExpiresIn { get; set; }
            
            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }
            
            [JsonPropertyName("scope")]
            public string? Scope { get; set; }
            
            [JsonPropertyName("token_type")]
            public string? TokenType { get; set; }
        }
    }
}