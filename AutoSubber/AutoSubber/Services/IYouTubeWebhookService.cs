using AutoSubber.Data;

namespace AutoSubber.Services
{
    /// <summary>
    /// Service for processing YouTube webhook events
    /// </summary>
    public interface IYouTubeWebhookService
    {
        /// <summary>
        /// Processes a YouTube webhook notification from PubSubHubbub
        /// </summary>
        /// <param name="xmlPayload">The XML payload from the webhook</param>
        /// <returns>True if processed successfully</returns>
        Task<bool> ProcessWebhookAsync(string xmlPayload);

        /// <summary>
        /// Gets unprocessed webhook events for processing
        /// </summary>
        /// <returns>List of unprocessed webhook events</returns>
        Task<List<WebhookEvent>> GetUnprocessedEventsAsync();

        /// <summary>
        /// Marks a webhook event as processed
        /// </summary>
        /// <param name="eventId">The webhook event ID</param>
        /// <returns>True if marked successfully</returns>
        Task<bool> MarkEventProcessedAsync(int eventId);
    }
}