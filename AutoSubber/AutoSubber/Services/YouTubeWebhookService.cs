using AutoSubber.Data;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;

namespace AutoSubber.Services
{
    /// <summary>
    /// Service for processing YouTube webhook events from PubSubHubbub
    /// </summary>
    public class YouTubeWebhookService : IYouTubeWebhookService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<YouTubeWebhookService> _logger;

        public YouTubeWebhookService(ApplicationDbContext context, ILogger<YouTubeWebhookService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Processes a YouTube webhook notification from PubSubHubbub
        /// </summary>
        public async Task<bool> ProcessWebhookAsync(string xmlPayload)
        {
            try
            {
                _logger.LogInformation("Processing YouTube webhook payload");

                // Parse the XML payload
                var (videoId, channelId, title) = ParseWebhookXml(xmlPayload);

                if (string.IsNullOrEmpty(videoId) || string.IsNullOrEmpty(channelId))
                {
                    _logger.LogWarning("Could not extract video ID or channel ID from webhook payload");
                    return false;
                }

                // Create webhook event record
                var webhookEvent = new WebhookEvent
                {
                    VideoId = videoId,
                    ChannelId = channelId,
                    Title = title,
                    RawPayload = xmlPayload,
                    ReceivedAt = DateTime.UtcNow,
                    IsProcessed = false
                };

                _context.WebhookEvents.Add(webhookEvent);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully queued webhook event for video {VideoId} from channel {ChannelId}", 
                    videoId, channelId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing YouTube webhook payload: {Payload}", xmlPayload);
                return false;
            }
        }

        /// <summary>
        /// Gets unprocessed webhook events for processing
        /// </summary>
        public async Task<List<WebhookEvent>> GetUnprocessedEventsAsync()
        {
            return await _context.WebhookEvents
                .Where(e => !e.IsProcessed)
                .OrderBy(e => e.ReceivedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Marks a webhook event as processed
        /// </summary>
        public async Task<bool> MarkEventProcessedAsync(int eventId)
        {
            try
            {
                var webhookEvent = await _context.WebhookEvents.FindAsync(eventId);
                if (webhookEvent == null)
                {
                    return false;
                }

                webhookEvent.IsProcessed = true;
                webhookEvent.ProcessedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking webhook event {EventId} as processed", eventId);
                return false;
            }
        }

        /// <summary>
        /// Parses YouTube webhook XML to extract video and channel information
        /// </summary>
        private (string videoId, string channelId, string? title) ParseWebhookXml(string xmlPayload)
        {
            try
            {
                var doc = XDocument.Parse(xmlPayload);

                // Define namespaces used in YouTube PubSubHubbub notifications
                var atomNs = XNamespace.Get("http://www.w3.org/2005/Atom");
                var ytNs = XNamespace.Get("http://www.youtube.com/xml/schemas/2015");

                // Extract video ID from yt:videoId element
                var videoId = doc.Descendants(ytNs + "videoId").FirstOrDefault()?.Value ?? string.Empty;

                // Extract channel ID from yt:channelId element
                var channelId = doc.Descendants(ytNs + "channelId").FirstOrDefault()?.Value ?? string.Empty;

                // Extract title from atom:title element (if available)
                var title = doc.Descendants(atomNs + "title").FirstOrDefault()?.Value;

                _logger.LogDebug("Parsed XML: VideoId={VideoId}, ChannelId={ChannelId}, Title={Title}", 
                    videoId, channelId, title);

                return (videoId, channelId, title);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing webhook XML payload");
                return (string.Empty, string.Empty, null);
            }
        }
    }
}