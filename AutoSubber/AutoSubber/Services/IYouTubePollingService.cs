using AutoSubber.Data;

namespace AutoSubber.Services
{
    /// <summary>
    /// Service for polling YouTube channels when PubSubHubbub fails or is disabled
    /// </summary>
    public interface IYouTubePollingService
    {
        /// <summary>
        /// Gets subscriptions that need fallback polling
        /// </summary>
        /// <returns>List of subscriptions requiring polling</returns>
        Task<List<Subscription>> GetSubscriptionsNeedingPollingAsync();

        /// <summary>
        /// Polls a channel for new videos and processes them
        /// </summary>
        /// <param name="subscription">The subscription to poll</param>
        /// <returns>Number of new videos found and processed</returns>
        Task<int> PollChannelForNewVideosAsync(Subscription subscription);

        /// <summary>
        /// Updates the last polled information for a subscription
        /// </summary>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="lastVideoId">Last video ID processed</param>
        /// <returns>True if updated successfully</returns>
        Task<bool> UpdateLastPolledInfoAsync(int subscriptionId, string? lastVideoId);
    }
}