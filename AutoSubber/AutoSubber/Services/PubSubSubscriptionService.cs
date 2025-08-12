using AutoSubber.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Web;

namespace AutoSubber.Services
{
    /// <summary>
    /// Implementation of PubSubHubbub subscription management service
    /// </summary>
    public class PubSubSubscriptionService : IPubSubSubscriptionService
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PubSubSubscriptionService> _logger;
        
        private const string PUBSUBHUBBUB_URL = "https://pubsubhubbub.appspot.com/subscribe";
        private const int DEFAULT_LEASE_SECONDS = 432000; // 5 days
        private const int MAX_RETRY_ATTEMPTS = 5;

        public PubSubSubscriptionService(
            HttpClient httpClient,
            ApplicationDbContext context,
            ILogger<PubSubSubscriptionService> logger)
        {
            _httpClient = httpClient;
            _context = context;
            _logger = logger;
        }

        public async Task<bool> SubscribeToChannelAsync(string channelId, string callbackUrl)
        {
            try
            {
                var feedUrl = $"https://www.youtube.com/xml/feeds/videos.xml?channel_id={channelId}";
                
                var formData = new List<KeyValuePair<string, string>>
                {
                    new("hub.callback", callbackUrl),
                    new("hub.topic", feedUrl),
                    new("hub.mode", "subscribe"),
                    new("hub.lease_seconds", DEFAULT_LEASE_SECONDS.ToString())
                };

                var formContent = new FormUrlEncodedContent(formData);

                _logger.LogInformation("Subscribing to PubSubHubbub for channel {ChannelId} with callback {CallbackUrl}", channelId, callbackUrl);

                var response = await _httpClient.PostAsync(PUBSUBHUBBUB_URL, formContent);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully initiated PubSubHubbub subscription for channel {ChannelId}", channelId);
                    return true;
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to subscribe to PubSubHubbub for channel {ChannelId}. Status: {StatusCode}, Response: {Response}", 
                        channelId, response.StatusCode, responseContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to PubSubHubbub for channel {ChannelId}", channelId);
                return false;
            }
        }

        public async Task<bool> UnsubscribeFromChannelAsync(string channelId, string callbackUrl)
        {
            try
            {
                var feedUrl = $"https://www.youtube.com/xml/feeds/videos.xml?channel_id={channelId}";
                
                var formData = new List<KeyValuePair<string, string>>
                {
                    new("hub.callback", callbackUrl),
                    new("hub.topic", feedUrl),
                    new("hub.mode", "unsubscribe")
                };

                var formContent = new FormUrlEncodedContent(formData);

                _logger.LogInformation("Unsubscribing from PubSubHubbub for channel {ChannelId}", channelId);

                var response = await _httpClient.PostAsync(PUBSUBHUBBUB_URL, formContent);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully initiated PubSubHubbub unsubscription for channel {ChannelId}", channelId);
                    return true;
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to unsubscribe from PubSubHubbub for channel {ChannelId}. Status: {StatusCode}, Response: {Response}", 
                        channelId, response.StatusCode, responseContent);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from PubSubHubbub for channel {ChannelId}", channelId);
                return false;
            }
        }

        public async Task<List<Subscription>> GetSubscriptionsNeedingAttentionAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                var renewalThreshold = now.AddHours(24); // Renew 24 hours before expiry

                // First get candidates from database (can be translated to SQL)
                var candidates = await _context.Subscriptions
                    .Where(s => s.IsIncluded && (
                        // Not yet subscribed to PubSubHubbub
                        !s.PubSubSubscribed ||
                        // Subscription expires within 24 hours
                        (s.PubSubLeaseExpiry.HasValue && s.PubSubLeaseExpiry.Value <= renewalThreshold) ||
                        // Failed subscriptions that might be eligible for retry
                        (s.PubSubSubscriptionAttempts > 0 && s.PubSubSubscriptionAttempts < MAX_RETRY_ATTEMPTS)
                    ))
                    .ToListAsync();

                // Then filter for retry logic in memory
                var result = candidates.Where(s =>
                    !s.PubSubSubscribed ||
                    (s.PubSubLeaseExpiry.HasValue && s.PubSubLeaseExpiry.Value <= renewalThreshold) ||
                    (s.PubSubSubscriptionAttempts > 0 && s.PubSubSubscriptionAttempts < MAX_RETRY_ATTEMPTS && 
                     ShouldRetrySubscription(s.PubSubLastAttempt, s.PubSubSubscriptionAttempts))
                ).ToList();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subscriptions needing attention");
                return new List<Subscription>();
            }
        }

        public async Task<bool> ProcessSubscriptionAsync(Subscription subscription, string callbackUrl)
        {
            try
            {
                // Check if we should retry based on exponential backoff
                if (subscription.PubSubSubscriptionAttempts > 0 && 
                    !ShouldRetrySubscription(subscription.PubSubLastAttempt, subscription.PubSubSubscriptionAttempts))
                {
                    _logger.LogDebug("Skipping subscription {SubscriptionId} - too early for retry", subscription.Id);
                    return false;
                }

                // Update attempt tracking
                subscription.PubSubSubscriptionAttempts++;
                subscription.PubSubLastAttempt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Try to subscribe
                var success = await SubscribeToChannelAsync(subscription.ChannelId, callbackUrl);

                if (success)
                {
                    // Calculate lease expiry (default 5 days minus 1 hour buffer)
                    var leaseExpiry = DateTime.UtcNow.AddSeconds(DEFAULT_LEASE_SECONDS - 3600);
                    await UpdateSubscriptionStatusAsync(subscription.Id, true, leaseExpiry);
                    
                    _logger.LogInformation("Successfully processed PubSubHubbub subscription for channel {ChannelId}", subscription.ChannelId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Failed to process PubSubHubbub subscription for channel {ChannelId}, attempt {Attempt}", 
                        subscription.ChannelId, subscription.PubSubSubscriptionAttempts);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing subscription for channel {ChannelId}", subscription.ChannelId);
                return false;
            }
        }

        public async Task<bool> UpdateSubscriptionStatusAsync(int subscriptionId, bool isSuccessful, DateTime? leaseExpiry = null)
        {
            try
            {
                var subscription = await _context.Subscriptions.FindAsync(subscriptionId);
                if (subscription == null)
                {
                    _logger.LogWarning("Subscription {SubscriptionId} not found", subscriptionId);
                    return false;
                }

                if (isSuccessful)
                {
                    subscription.PubSubSubscribed = true;
                    subscription.PubSubLeaseExpiry = leaseExpiry;
                    subscription.PubSubSubscriptionAttempts = 0; // Reset attempts on success
                    subscription.PubSubLastAttempt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated subscription {SubscriptionId} status: successful={IsSuccessful}, expiry={LeaseExpiry}", 
                    subscriptionId, isSuccessful, leaseExpiry);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating subscription {SubscriptionId} status", subscriptionId);
                return false;
            }
        }

        /// <summary>
        /// Determines if a subscription should be retried based on exponential backoff
        /// </summary>
        private static bool ShouldRetrySubscription(DateTime? lastAttempt, int attemptCount)
        {
            if (!lastAttempt.HasValue || attemptCount >= MAX_RETRY_ATTEMPTS)
                return false;

            // Exponential backoff: 2^attempt minutes
            var backoffMinutes = Math.Pow(2, attemptCount);
            var nextRetryTime = lastAttempt.Value.AddMinutes(backoffMinutes);
            
            return DateTime.UtcNow >= nextRetryTime;
        }
    }
}