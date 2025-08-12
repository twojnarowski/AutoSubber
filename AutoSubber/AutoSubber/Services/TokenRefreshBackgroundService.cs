using AutoSubber.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoSubber.Services
{
    /// <summary>
    /// Background service that periodically checks for and refreshes expiring YouTube tokens
    /// </summary>
    public class TokenRefreshBackgroundService : BackgroundService
    {
        private readonly ILogger<TokenRefreshBackgroundService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15); // Check every 15 minutes

        public TokenRefreshBackgroundService(
            ILogger<TokenRefreshBackgroundService> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Token refresh background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndRefreshTokensAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in token refresh background service");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
            }

            _logger.LogInformation("Token refresh background service stopped");
        }

        private async Task CheckAndRefreshTokensAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tokenRefreshService = scope.ServiceProvider.GetRequiredService<IYouTubeTokenRefreshService>();

            try
            {
                // Get all users who have Google tokens and automation enabled
                var usersWithTokens = await context.Users
                    .Where(u => !string.IsNullOrEmpty(u.EncryptedRefreshToken) && !u.AutomationDisabled)
                    .ToListAsync();

                _logger.LogDebug("Checking tokens for {UserCount} users", usersWithTokens.Count);

                var refreshedCount = 0;
                var failedCount = 0;

                foreach (var user in usersWithTokens)
                {
                    try
                    {
                        if (tokenRefreshService.TokenNeedsRefresh(user))
                        {
                            _logger.LogInformation("Refreshing token for user {UserId}", user.Id);
                            
                            var success = await tokenRefreshService.RefreshUserTokenAsync(user);
                            
                            if (success)
                            {
                                refreshedCount++;
                            }
                            else
                            {
                                failedCount++;
                                // Disable automation for this user after failed refresh
                                await tokenRefreshService.DisableAutomationAsync(user);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error refreshing token for user {UserId}", user.Id);
                        failedCount++;
                        
                        try
                        {
                            // Disable automation for this user after error
                            await tokenRefreshService.DisableAutomationAsync(user);
                        }
                        catch (Exception disableEx)
                        {
                            _logger.LogError(disableEx, "Error disabling automation for user {UserId} after token refresh failure", user.Id);
                        }
                    }
                }

                if (refreshedCount > 0 || failedCount > 0)
                {
                    _logger.LogInformation("Token refresh cycle completed: {RefreshedCount} refreshed, {FailedCount} failed", 
                        refreshedCount, failedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh cycle");
            }
        }
    }
}