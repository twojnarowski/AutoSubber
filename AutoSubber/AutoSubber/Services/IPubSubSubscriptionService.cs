using AutoSubber.Data;

namespace AutoSubber.Services
{
    /// <summary>
    /// Service for managing PubSubHubbub subscriptions for YouTube channels
    /// </summary>
    public interface IPubSubSubscriptionService
    {
        /// <summary>
        /// Subscribes to PubSubHubbub notifications for a specific channel
        /// </summary>
        /// <param name="channelId">The YouTube channel ID</param>
        /// <param name="callbackUrl">The webhook callback URL</param>
        /// <returns>True if subscription was successful, false otherwise</returns>
        Task<bool> SubscribeToChannelAsync(string channelId, string callbackUrl);

        /// <summary>
        /// Unsubscribes from PubSubHubbub notifications for a specific channel
        /// </summary>
        /// <param name="channelId">The YouTube channel ID</param>
        /// <param name="callbackUrl">The webhook callback URL</param>
        /// <returns>True if unsubscription was successful, false otherwise</returns>
        Task<bool> UnsubscribeFromChannelAsync(string channelId, string callbackUrl);

        /// <summary>
        /// Gets all subscriptions that need PubSubHubbub subscription or renewal
        /// </summary>
        /// <returns>List of subscriptions that need attention</returns>
        Task<List<Subscription>> GetSubscriptionsNeedingAttentionAsync();

        /// <summary>
        /// Processes a subscription for PubSubHubbub subscription or renewal with retry logic
        /// </summary>
        /// <param name="subscription">The subscription to process</param>
        /// <param name="callbackUrl">The webhook callback URL</param>
        /// <returns>True if successful, false if retry is needed</returns>
        Task<bool> ProcessSubscriptionAsync(Subscription subscription, string callbackUrl);

        /// <summary>
        /// Updates the subscription status after a successful PubSubHubbub operation
        /// </summary>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <param name="isSuccessful">Whether the operation was successful</param>
        /// <param name="leaseExpiry">The lease expiry time if successful</param>
        /// <returns>True if updated successfully</returns>
        Task<bool> UpdateSubscriptionStatusAsync(int subscriptionId, bool isSuccessful, DateTime? leaseExpiry = null);
    }
}