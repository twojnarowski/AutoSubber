using AutoSubber.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoSubber.Services
{
    /// <summary>
    /// Implementation of video processing service
    /// </summary>
    public class VideoProcessingService : IVideoProcessingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<VideoProcessingService> _logger;
        private readonly IYouTubePlaylistService _playlistService;
        private readonly IYouTubeWebhookService _webhookService;

        public VideoProcessingService(
            ApplicationDbContext context,
            ILogger<VideoProcessingService> logger,
            IYouTubePlaylistService playlistService,
            IYouTubeWebhookService webhookService)
        {
            _context = context;
            _logger = logger;
            _playlistService = playlistService;
            _webhookService = webhookService;
        }

        public async Task<int> ProcessUnprocessedWebhookEventsAsync()
        {
            try
            {
                var unprocessedEvents = await _webhookService.GetUnprocessedEventsAsync();

                if (unprocessedEvents.Count == 0)
                {
                    _logger.LogDebug("No unprocessed webhook events found");
                    return 0;
                }

                _logger.LogInformation("Processing {Count} unprocessed webhook events", unprocessedEvents.Count);

                var totalProcessed = 0;

                foreach (var webhookEvent in unprocessedEvents)
                {
                    try
                    {
                        var usersProcessed = await ProcessVideoDiscoveryAsync(
                            webhookEvent.VideoId,
                            webhookEvent.ChannelId,
                            webhookEvent.Title,
                            "Webhook");

                        if (usersProcessed > 0)
                        {
                            totalProcessed++;
                            _logger.LogDebug("Successfully processed webhook event {EventId} for video {VideoId} - added to {UserCount} user playlists",
                                webhookEvent.Id, webhookEvent.VideoId, usersProcessed);
                        }

                        // Mark the webhook event as processed regardless of outcome
                        await _webhookService.MarkEventProcessedAsync(webhookEvent.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing webhook event {EventId} for video {VideoId}",
                            webhookEvent.Id, webhookEvent.VideoId);

                        // Still mark as processed to avoid infinite retry
                        await _webhookService.MarkEventProcessedAsync(webhookEvent.Id);
                    }
                }

                _logger.LogInformation("Completed processing webhook events: {ProcessedCount}/{TotalCount} videos processed successfully",
                    totalProcessed, unprocessedEvents.Count);

                return totalProcessed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during webhook event processing");
                return 0;
            }
        }

        public async Task<int> ProcessVideoDiscoveryAsync(string videoId, string channelId, string? title = null, string source = "Unknown")
        {
            try
            {
                if (string.IsNullOrEmpty(videoId) || string.IsNullOrEmpty(channelId))
                {
                    _logger.LogWarning("Invalid video ID or channel ID provided for processing");
                    return 0;
                }

                // Find all users subscribed to this channel who have automation enabled
                var subscribedUsers = await _context.Subscriptions
                    .Include(s => s.User)
                    .Where(s => s.ChannelId == channelId &&
                               s.IsIncluded &&
                               !s.User.AutomationDisabled &&
                               s.User.AutoWatchLaterPlaylistId != null &&
                               s.User.EncryptedAccessToken != null)
                    .Select(s => s.User)
                    .Distinct()
                    .ToListAsync();

                if (subscribedUsers.Count == 0)
                {
                    _logger.LogDebug("No users found with active subscriptions to channel {ChannelId} for video {VideoId}",
                        channelId, videoId);
                    return 0;
                }

                _logger.LogDebug("Found {UserCount} users subscribed to channel {ChannelId} for video {VideoId}",
                    subscribedUsers.Count, channelId, videoId);

                var successfullyProcessed = 0;

                foreach (var user in subscribedUsers)
                {
                    try
                    {
                        // Check if this video has already been processed for this user
                        if (await IsVideoAlreadyProcessedAsync(user.Id, videoId))
                        {
                            _logger.LogDebug("Video {VideoId} already processed for user {UserId}, skipping",
                                videoId, user.Id);
                            continue;
                        }

                        // Attempt to add video to user's playlist
                        var addedSuccessfully = await _playlistService.AddVideoToPlaylistAsync(user, videoId, channelId, title);

                        // Record the processing attempt
                        await RecordProcessedVideoAsync(
                            user.Id,
                            videoId,
                            channelId,
                            title,
                            source,
                            addedSuccessfully,
                            addedSuccessfully ? null : "Failed to add to playlist");

                        if (addedSuccessfully)
                        {
                            successfullyProcessed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing video {VideoId} for user {UserId}",
                            videoId, user.Id);

                        // Record the failed attempt
                        await RecordProcessedVideoAsync(
                            user.Id,
                            videoId,
                            channelId,
                            title,
                            source,
                            false,
                            ex.Message);
                    }
                }

                _logger.LogInformation("Processed video {VideoId} (Title: {Title}) from channel {ChannelId}: {SuccessCount}/{TotalCount} users successful",
                    videoId, title ?? "Unknown", channelId, successfullyProcessed, subscribedUsers.Count);

                return successfullyProcessed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during video discovery processing for video {VideoId}", videoId);
                return 0;
            }
        }

        public async Task<bool> IsVideoAlreadyProcessedAsync(string userId, string videoId)
        {
            try
            {
                return await _context.ProcessedVideos
                    .AnyAsync(pv => pv.UserId == userId && pv.VideoId == videoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if video {VideoId} is already processed for user {UserId}",
                    videoId, userId);
                return false;
            }
        }

        public async Task<bool> RecordProcessedVideoAsync(string userId, string videoId, string channelId, string? title, string source, bool addedToPlaylist, string? errorMessage = null, int retryAttempts = 0)
        {
            try
            {
                var processedVideo = new ProcessedVideo
                {
                    UserId = userId,
                    VideoId = videoId,
                    ChannelId = channelId,
                    Title = title,
                    Source = source,
                    AddedToPlaylist = addedToPlaylist,
                    ErrorMessage = errorMessage,
                    RetryAttempts = retryAttempts,
                    ProcessedAt = DateTime.UtcNow
                };

                _context.ProcessedVideos.Add(processedVideo);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Recorded processed video {VideoId} for user {UserId}: Success={Success}, Retries={Retries}",
                    videoId, userId, addedToPlaylist, retryAttempts);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording processed video {VideoId} for user {UserId}",
                    videoId, userId);
                return false;
            }
        }
    }
}