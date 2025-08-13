using AutoSubber.Data;

namespace AutoSubber.Services
{
    /// <summary>
    /// Service for refreshing YouTube OAuth tokens
    /// </summary>
    public interface IYouTubeTokenRefreshService
    {
        /// <summary>
        /// Refreshes the access token for a user using their stored refresh token
        /// </summary>
        /// <param name="user">The user whose token should be refreshed</param>
        /// <returns>True if refresh was successful, false otherwise</returns>
        Task<bool> RefreshUserTokenAsync(ApplicationUser user);
        
        /// <summary>
        /// Checks if a user's token needs to be refreshed (expires within the buffer time)
        /// </summary>
        /// <param name="user">The user to check</param>
        /// <param name="bufferMinutes">Minutes before expiry to consider token as needing refresh (default: 30)</param>
        /// <returns>True if token needs refresh, false otherwise</returns>
        bool TokenNeedsRefresh(ApplicationUser user, int bufferMinutes = 30);
        
        /// <summary>
        /// Marks a user as having automation disabled due to token refresh failure
        /// </summary>
        /// <param name="user">The user to disable automation for</param>
        Task DisableAutomationAsync(ApplicationUser user);
    }
}