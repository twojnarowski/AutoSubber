using AutoSubber.Data;

namespace AutoSubber.Services
{
    /// <summary>
    /// Service for managing YouTube channel subscriptions
    /// </summary>
    public interface IYouTubeSubscriptionService
    {
        /// <summary>
        /// Fetches and stores all YouTube subscriptions for the user
        /// </summary>
        /// <param name="user">The user to fetch subscriptions for</param>
        /// <returns>Number of subscriptions fetched and stored, null if failed</returns>
        Task<int?> FetchAndStoreSubscriptionsAsync(ApplicationUser user);

        /// <summary>
        /// Gets all subscriptions for a user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>List of subscriptions</returns>
        Task<List<Subscription>> GetUserSubscriptionsAsync(string userId);

        /// <summary>
        /// Updates the inclusion status of a subscription
        /// </summary>
        /// <param name="subscriptionId">The subscription ID</param>
        /// <param name="isIncluded">Whether the subscription should be included for automation</param>
        /// <param name="userId">The user ID (for security)</param>
        /// <returns>True if updated successfully</returns>
        Task<bool> UpdateSubscriptionInclusionAsync(int subscriptionId, bool isIncluded, string userId);

        /// <summary>
        /// Updates the inclusion status for all subscriptions belonging to a user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="isIncluded">Whether subscriptions should be included for automation</param>
        /// <returns>Number of subscriptions updated</returns>
        Task<int> UpdateAllSubscriptionsInclusionAsync(string userId, bool isIncluded);
    }
}