using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoSubber.Data
{
    /// <summary>
    /// Represents a video that has been processed and added to a user's playlist
    /// </summary>
    public class ProcessedVideo
    {
        /// <summary>
        /// Primary key
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to ApplicationUser
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// YouTube video ID
        /// </summary>
        [Required]
        [StringLength(50)]
        public string VideoId { get; set; } = string.Empty;

        /// <summary>
        /// YouTube channel ID that published the video
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
        /// When the video was processed and added to playlist
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the video was successfully added to the playlist
        /// </summary>
        public bool AddedToPlaylist { get; set; }

        /// <summary>
        /// Error message if adding to playlist failed
        /// </summary>
        [StringLength(1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Number of retry attempts made if initial insertion failed
        /// </summary>
        public int RetryAttempts { get; set; } = 0;

        /// <summary>
        /// Navigation property to user
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; } = null!;

        /// <summary>
        /// Source of video discovery (Webhook, Polling)
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Source { get; set; } = string.Empty;
    }
}