using AutoSubber.Data;

namespace AutoSubber.Services
{
    /// <summary>
    /// Service for processing discovered videos and adding them to user playlists
    /// </summary>
    public interface IVideoProcessingService
    {
        /// <summary>
        /// Processes unprocessed webhook events and adds videos to playlists
        /// </summary>
        /// <returns>Number of videos successfully processed</returns>
        Task<int> ProcessUnprocessedWebhookEventsAsync();

        /// <summary>
        /// Processes a single video discovery and adds it to the user's playlist if applicable
        /// </summary>
        /// <param name="videoId">YouTube video ID</param>
        /// <param name="channelId">YouTube channel ID</param>
        /// <param name="title">Optional video title</param>
        /// <param name="source">Source of discovery (Webhook, Polling)</param>
        /// <returns>Number of users who had the video added to their playlist</returns>
        Task<int> ProcessVideoDiscoveryAsync(string videoId, string channelId, string? title = null, string source = "Unknown");

        /// <summary>
        /// Checks if a video has already been processed for a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="videoId">Video ID</param>
        /// <returns>True if already processed, false otherwise</returns>
        Task<bool> IsVideoAlreadyProcessedAsync(string userId, string videoId);

        /// <summary>
        /// Records a processed video in the database
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="videoId">Video ID</param>
        /// <param name="channelId">Channel ID</param>
        /// <param name="title">Optional video title</param>
        /// <param name="source">Source of discovery</param>
        /// <param name="addedToPlaylist">Whether video was successfully added to playlist</param>
        /// <param name="errorMessage">Error message if addition failed</param>
        /// <param name="retryAttempts">Number of retry attempts made</param>
        /// <returns>True if recorded successfully</returns>
        Task<bool> RecordProcessedVideoAsync(string userId, string videoId, string channelId, string? title, string source, bool addedToPlaylist, string? errorMessage = null, int retryAttempts = 0);
    }
}