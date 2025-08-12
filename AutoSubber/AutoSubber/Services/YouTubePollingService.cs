using AutoSubber.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.EntityFrameworkCore;

namespace AutoSubber.Services
{
    /// <summary>
    /// Implementation of YouTube polling service for fallback when PubSubHubbub fails
    /// </summary>
    public class YouTubePollingService : IYouTubePollingService
    {
        private readonly ILogger<YouTubePollingService> _logger;
        private readonly ITokenEncryptionService _tokenEncryption;
        private readonly ApplicationDbContext _context;
        private readonly IYouTubePlaylistService _playlistService;
        private readonly IConfiguration _configuration;

        public YouTubePollingService(
            ILogger<YouTubePollingService> logger,
            ITokenEncryptionService tokenEncryption,
            ApplicationDbContext context,
            IYouTubePlaylistService playlistService,
            IConfiguration configuration)
        {
            _logger = logger;
            _tokenEncryption = tokenEncryption;
            _context = context;
            _playlistService = playlistService;
            _configuration = configuration;
        }

        public async Task<List<Subscription>> GetSubscriptionsNeedingPollingAsync()
        {
            var pollingInterval = TimeSpan.FromHours(_configuration.GetValue<double>("YouTubePolling:IntervalHours", 1.0));
            var cutoffTime = DateTime.UtcNow.Subtract(pollingInterval);

            var subscriptionsNeedingPolling = await _context.Subscriptions
                .Include(s => s.User)
                .Where(s => s.IsIncluded && 
                           s.PollingEnabled &&
                           s.User.EncryptedAccessToken != null &&
                           (
                               // Channel where PubSub is not working
                               (!s.PubSubSubscribed) ||
                               // Channel where PubSub has expired
                               (s.PubSubLeaseExpiry.HasValue && s.PubSubLeaseExpiry < DateTime.UtcNow) ||
                               // Channel that hasn't been polled recently
                               (!s.LastPolledAt.HasValue || s.LastPolledAt < cutoffTime)
                           ))
                .ToListAsync();

            _logger.LogDebug("Found {Count} subscriptions needing polling", subscriptionsNeedingPolling.Count);
            return subscriptionsNeedingPolling;
        }

        public async Task<int> PollChannelForNewVideosAsync(Subscription subscription)
        {
            try
            {
                if (subscription.User?.EncryptedAccessToken == null)
                {
                    _logger.LogWarning("No access token available for user {UserId}", subscription.UserId);
                    return 0;
                }

                // Decrypt the access token
                var accessToken = _tokenEncryption.Decrypt(subscription.User.EncryptedAccessToken);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("Failed to decrypt access token for user {UserId}", subscription.UserId);
                    return 0;
                }

                // Create YouTube service
                var credential = GoogleCredential.FromAccessToken(accessToken);
                var youtubeService = new YouTubeService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "AutoSubber"
                });

                // Get recent videos from the channel
                var searchRequest = youtubeService.Search.List("snippet");
                searchRequest.ChannelId = subscription.ChannelId;
                searchRequest.Type = "video";
                searchRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
                searchRequest.MaxResults = 10; // Check last 10 videos
                searchRequest.PublishedAfterDateTimeOffset = DateTime.UtcNow.Subtract(TimeSpan.FromDays(7)); // Only check videos from last week

                var searchResponse = await searchRequest.ExecuteAsync();
                var newVideosProcessed = 0;

                if (searchResponse.Items != null)
                {
                    // Process videos in chronological order (oldest first)
                    var videos = searchResponse.Items.OrderBy(v => v.Snippet.PublishedAtDateTimeOffset).ToList();
                    
                    foreach (var video in videos)
                    {
                        var videoId = video.Id.VideoId;
                        
                        // Skip if this is the last video we already processed
                        if (!string.IsNullOrEmpty(subscription.LastPolledVideoId) && 
                            videoId == subscription.LastPolledVideoId)
                        {
                            break; // Stop here as we've reached already processed videos
                        }

                        // Check if we've already processed this video via webhook
                        var existingEvent = await _context.WebhookEvents
                            .Where(e => e.VideoId == videoId && e.ChannelId == subscription.ChannelId)
                            .FirstOrDefaultAsync();

                        if (existingEvent != null)
                        {
                            _logger.LogDebug("Video {VideoId} already processed via webhook, skipping", videoId);
                            continue;
                        }

                        // Create a webhook event for this video (simulating what would come from PubSub)
                        var webhookEvent = new WebhookEvent
                        {
                            VideoId = videoId,
                            ChannelId = subscription.ChannelId,
                            Title = video.Snippet.Title,
                            ReceivedAt = DateTime.UtcNow,
                            IsProcessed = false
                        };

                        _context.WebhookEvents.Add(webhookEvent);
                        newVideosProcessed++;

                        // Update the last processed video
                        subscription.LastPolledVideoId = videoId;

                        _logger.LogInformation("Found new video via polling: {VideoId} from channel {ChannelId}", 
                            videoId, subscription.ChannelId);
                    }
                }

                // Update the last polled timestamp
                subscription.LastPolledAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();

                _logger.LogDebug("Polling completed for channel {ChannelId}, found {Count} new videos", 
                    subscription.ChannelId, newVideosProcessed);

                return newVideosProcessed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling channel {ChannelId} for user {UserId}", 
                    subscription.ChannelId, subscription.UserId);
                return 0;
            }
        }

        public async Task<bool> UpdateLastPolledInfoAsync(int subscriptionId, string? lastVideoId)
        {
            try
            {
                var subscription = await _context.Subscriptions.FindAsync(subscriptionId);
                if (subscription == null)
                {
                    _logger.LogWarning("Subscription {SubscriptionId} not found", subscriptionId);
                    return false;
                }

                subscription.LastPolledAt = DateTime.UtcNow;
                subscription.LastPolledVideoId = lastVideoId;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last polled info for subscription {SubscriptionId}", subscriptionId);
                return false;
            }
        }
    }
}