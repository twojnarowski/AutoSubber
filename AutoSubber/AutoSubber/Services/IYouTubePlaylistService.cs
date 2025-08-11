using AutoSubber.Data;

namespace AutoSubber.Services
{
    /// <summary>
    /// Service for managing YouTube playlists
    /// </summary>
    public interface IYouTubePlaylistService
    {
        /// <summary>
        /// Creates an Auto Watch Later playlist for the user
        /// </summary>
        /// <param name="user">The user to create the playlist for</param>
        /// <returns>The playlist ID if successful, null if failed</returns>
        Task<string?> CreateAutoWatchLaterPlaylistAsync(ApplicationUser user);
    }
}