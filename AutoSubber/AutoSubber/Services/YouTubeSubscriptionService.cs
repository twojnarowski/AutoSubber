using AutoSubber.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.EntityFrameworkCore;

namespace AutoSubber.Services
{
    /// <summary>
    /// Implementation of YouTube subscription management service
    /// </summary>
    public class YouTubeSubscriptionService : IYouTubeSubscriptionService
    {
        private readonly ILogger<YouTubeSubscriptionService> _logger;
        private readonly ITokenEncryptionService _tokenEncryption;
        private readonly ApplicationDbContext _context;
        private readonly IPubSubSubscriptionService _pubSubService;
        private readonly IConfiguration _configuration;

        public YouTubeSubscriptionService(
            ILogger<YouTubeSubscriptionService> logger,
            ITokenEncryptionService tokenEncryption,
            ApplicationDbContext context,
            IPubSubSubscriptionService pubSubService,
            IConfiguration configuration)
        {
            _logger = logger;
            _tokenEncryption = tokenEncryption;
            _context = context;
            _pubSubService = pubSubService;
            _configuration = configuration;
        }

        public async Task<int?> FetchAndStoreSubscriptionsAsync(ApplicationUser user)
        {
            try
            {
                // Check if automation is disabled for this user
                if (user.AutomationDisabled)
                {
                    _logger.LogWarning("User {UserId} has automation disabled - skipping subscription fetch. User needs to re-authenticate.", user.Id);
                    return null;
                }

                // Validate user has required tokens
                if (string.IsNullOrEmpty(user.EncryptedAccessToken))
                {
                    _logger.LogWarning("User {UserId} does not have an access token", user.Id);
                    return null;
                }

                // Decrypt the access token
                var accessToken = _tokenEncryption.Decrypt(user.EncryptedAccessToken);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("Failed to decrypt access token for user {UserId}", user.Id);
                    return null;
                }

                // Create YouTube service with OAuth2 credentials
                var credential = GoogleCredential.FromAccessToken(accessToken);
                var youtubeService = new YouTubeService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "AutoSubber"
                });

                // Clear existing subscriptions for this user
                var existingSubscriptions = await _context.Subscriptions
                    .Where(s => s.UserId == user.Id)
                    .ToListAsync();
                
                _context.Subscriptions.RemoveRange(existingSubscriptions);

                var subscriptionCount = 0;
                string? pageToken = null;

                do
                {
                    // Create the subscriptions list request
                    var subscriptionsRequest = youtubeService.Subscriptions.List("snippet");
                    subscriptionsRequest.Mine = true;
                    subscriptionsRequest.MaxResults = 50; // Maximum allowed by YouTube API
                    subscriptionsRequest.PageToken = pageToken;

                    // Execute the request
                    var subscriptionsResponse = await subscriptionsRequest.ExecuteAsync();

                    if (subscriptionsResponse.Items != null)
                    {
                        foreach (var subscription in subscriptionsResponse.Items)
                        {
                            if (subscription.Snippet?.ChannelId != null && 
                                subscription.Snippet?.Title != null)
                            {
                                // Get the best quality thumbnail URL available
                                var thumbnailUrl = GetBestThumbnailUrl(subscription.Snippet.Thumbnails);

                                var newSubscription = new Subscription
                                {
                                    UserId = user.Id,
                                    ChannelId = subscription.Snippet.ChannelId,
                                    Title = subscription.Snippet.Title,
                                    ThumbnailUrl = thumbnailUrl,
                                    IsIncluded = true, // Default to included
                                    CreatedAt = DateTime.UtcNow
                                };

                                _context.Subscriptions.Add(newSubscription);
                                subscriptionCount++;
                            }
                        }
                    }

                    // Get the next page token for pagination
                    pageToken = subscriptionsResponse.NextPageToken;

                } while (!string.IsNullOrEmpty(pageToken));

                // Save all changes to database
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully fetched and stored {Count} subscriptions for user {UserId}", 
                    subscriptionCount, user.Id);

                // Trigger PubSub subscriptions for new channels
                await TriggerPubSubSubscriptionsAsync();
                
                return subscriptionCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching subscriptions for user {UserId}", user.Id);
                return null;
            }
        }

        public async Task<List<Subscription>> GetUserSubscriptionsAsync(string userId)
        {
            try
            {
                return await _context.Subscriptions
                    .Where(s => s.UserId == userId)
                    .OrderBy(s => s.Title)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subscriptions for user {UserId}", userId);
                return new List<Subscription>();
            }
        }

        public async Task<bool> UpdateSubscriptionInclusionAsync(int subscriptionId, bool isIncluded, string userId)
        {
            try
            {
                var subscription = await _context.Subscriptions
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.UserId == userId);

                if (subscription == null)
                {
                    _logger.LogWarning("Subscription {SubscriptionId} not found for user {UserId}", subscriptionId, userId);
                    return false;
                }

                subscription.IsIncluded = isIncluded;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated subscription {SubscriptionId} inclusion to {IsIncluded} for user {UserId}", 
                    subscriptionId, isIncluded, userId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating subscription {SubscriptionId} for user {UserId}", subscriptionId, userId);
                return false;
            }
        }

        public async Task<int> UpdateAllSubscriptionsInclusionAsync(string userId, bool isIncluded)
        {
            try
            {
                var subscriptions = await _context.Subscriptions
                    .Where(s => s.UserId == userId)
                    .ToListAsync();

                var updateCount = 0;
                foreach (var subscription in subscriptions)
                {
                    if (subscription.IsIncluded != isIncluded)
                    {
                        subscription.IsIncluded = isIncluded;
                        updateCount++;
                    }
                }

                if (updateCount > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated {Count} subscriptions inclusion to {IsIncluded} for user {UserId}", 
                        updateCount, isIncluded, userId);
                }

                return updateCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating all subscriptions for user {UserId}", userId);
                return 0;
            }
        }

        /// <summary>
        /// Triggers PubSub subscriptions for channels that need them
        /// </summary>
        private async Task TriggerPubSubSubscriptionsAsync()
        {
            try
            {
                var baseUrl = _configuration["BaseUrl"];
                if (string.IsNullOrEmpty(baseUrl))
                {
                    _logger.LogWarning("BaseUrl not configured, cannot trigger PubSub subscriptions");
                    return;
                }

                var callbackUrl = $"{baseUrl.TrimEnd('/')}/api/youtube/webhook";
                var subscriptionsNeedingAttention = await _pubSubService.GetSubscriptionsNeedingAttentionAsync();

                _logger.LogInformation("Triggering PubSub subscriptions for {Count} channels", subscriptionsNeedingAttention.Count);

                foreach (var subscription in subscriptionsNeedingAttention)
                {
                    try
                    {
                        await _pubSubService.ProcessSubscriptionAsync(subscription, callbackUrl);
                        // Add small delay to avoid overwhelming the service
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error triggering PubSub subscription for channel {ChannelId}", subscription.ChannelId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering PubSub subscriptions");
            }
        }

        /// <summary>
        /// Gets the best quality thumbnail URL from YouTube API thumbnails
        /// </summary>
        /// <param name="thumbnails">Thumbnail collection from YouTube API</param>
        /// <returns>Best available thumbnail URL, or null if none available</returns>
        private static string? GetBestThumbnailUrl(Google.Apis.YouTube.v3.Data.ThumbnailDetails? thumbnails)
        {
            if (thumbnails == null) return null;

            // Prefer higher quality thumbnails, fallback to lower quality
            return thumbnails.High?.Url ?? 
                   thumbnails.Medium?.Url ?? 
                   thumbnails.Default__?.Url;
        }
    }
}