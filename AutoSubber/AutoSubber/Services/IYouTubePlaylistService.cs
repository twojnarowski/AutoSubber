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

        /// <summary>
        /// Adds a video to the user's Auto Watch Later playlist
        /// </summary>
        /// <param name="user">The user whose playlist to add to</param>
        /// <param name="videoId">YouTube video ID to add</param>
        /// <param name="channelId">YouTube channel ID that published the video</param>
        /// <param name="videoTitle">Optional video title for logging</param>
        /// <returns>True if video was successfully added, false otherwise</returns>
        Task<bool> AddVideoToPlaylistAsync(ApplicationUser user, string videoId, string channelId, string? videoTitle = null);
    }
}