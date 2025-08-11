using Microsoft.AspNetCore.Identity;

namespace AutoSubber.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// Google subject identifier (sub claim)
        /// </summary>
        public string? GoogleSubject { get; set; }
        
        /// <summary>
        /// Display name from Google profile
        /// </summary>
        public string? DisplayName { get; set; }
        
        /// <summary>
        /// Encrypted Google access token for YouTube API access
        /// </summary>
        public string? EncryptedAccessToken { get; set; }
        
        /// <summary>
        /// Encrypted Google refresh token for token renewal
        /// </summary>
        public string? EncryptedRefreshToken { get; set; }
        
        /// <summary>
        /// When the access token expires (UTC)
        /// </summary>
        public DateTime? TokenExpiresAt { get; set; }
        
        /// <summary>
        /// YouTube Auto Watch Later playlist ID
        /// </summary>
        public string? AutoWatchLaterPlaylistId { get; set; }
    }
}
