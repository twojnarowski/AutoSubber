using AutoSubber.Data;

namespace AutoSubber.Services
{
    /// <summary>
    /// Interface for diagnostics service providing admin insights
    /// </summary>
    public interface IDiagnosticsService
    {
        /// <summary>
        /// Get API quota usage for the last N days
        /// </summary>
        /// <param name="days">Number of days to look back</param>
        /// <returns>List of quota usage records</returns>
        Task<List<ApiQuotaUsage>> GetQuotaUsageAsync(int days = 30);

        /// <summary>
        /// Get failed processed videos with error details
        /// </summary>
        /// <param name="days">Number of days to look back</param>
        /// <returns>List of failed video processing records</returns>
        Task<List<ProcessedVideo>> GetFailedJobsAsync(int days = 30);

        /// <summary>
        /// Get webhook subscription health status
        /// </summary>
        /// <returns>List of subscriptions with their health status</returns>
        Task<List<Subscription>> GetWebhookHealthAsync();

        /// <summary>
        /// Get unprocessed webhook events (indicating processing delays)
        /// </summary>
        /// <param name="hours">Number of hours to look back</param>
        /// <returns>List of unprocessed webhook events</returns>
        Task<List<WebhookEvent>> GetUnprocessedWebhookEventsAsync(int hours = 24);

        /// <summary>
        /// Update or create quota usage record for today
        /// </summary>
        /// <param name="serviceName">Name of the API service</param>
        /// <param name="requestsUsed">Number of requests used</param>
        /// <param name="quotaLimit">Daily quota limit</param>
        /// <param name="costUnitsUsed">Cost units used</param>
        /// <param name="costUnitLimit">Cost unit limit</param>
        /// <returns>Updated quota usage record</returns>
        Task<ApiQuotaUsage> UpdateQuotaUsageAsync(string serviceName, int requestsUsed, int quotaLimit, long costUnitsUsed = 0, long costUnitLimit = 0);

        /// <summary>
        /// Get summary statistics for admin dashboard
        /// </summary>
        /// <returns>Dictionary with key metrics</returns>
        Task<Dictionary<string, object>> GetSummaryStatsAsync();
    }
}