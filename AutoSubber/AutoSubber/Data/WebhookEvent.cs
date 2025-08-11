using System.ComponentModel.DataAnnotations;

namespace AutoSubber.Data
{
    /// <summary>
    /// Represents a YouTube webhook event received from PubSubHubbub
    /// </summary>
    public class WebhookEvent
    {
        /// <summary>
        /// Primary key
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// YouTube video ID from the notification
        /// </summary>
        [Required]
        [StringLength(50)]
        public string VideoId { get; set; } = string.Empty;

        /// <summary>
        /// YouTube channel ID from the notification
        /// </summary>
        [Required]
        [StringLength(100)]
        public string ChannelId { get; set; } = string.Empty;

        /// <summary>
        /// Video title if available
        /// </summary>
        [StringLength(500)]
        public string? Title { get; set; }

        /// <summary>
        /// When the webhook event was received
        /// </summary>
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this event has been processed
        /// </summary>
        public bool IsProcessed { get; set; } = false;

        /// <summary>
        /// When the event was processed (if applicable)
        /// </summary>
        public DateTime? ProcessedAt { get; set; }

        /// <summary>
        /// Raw XML payload for debugging purposes
        /// </summary>
        public string? RawPayload { get; set; }
    }
}