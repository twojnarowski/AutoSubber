using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoSubber.Data
{
    /// <summary>
    /// Represents a YouTube channel subscription for a user
    /// </summary>
    public class Subscription
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
        /// YouTube channel ID
        /// </summary>
        [Required]
        [StringLength(100)]
        public string ChannelId { get; set; } = string.Empty;

        /// <summary>
        /// Channel title/name
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Whether this subscription is included for automation
        /// </summary>
        public bool IsIncluded { get; set; } = true;

        /// <summary>
        /// When this subscription record was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property to user
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; } = null!;
    }
}