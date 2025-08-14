using System.ComponentModel.DataAnnotations;

namespace AutoSubber.Data
{
    /// <summary>
    /// Represents daily API quota usage tracking for various Google services
    /// </summary>
    public class ApiQuotaUsage
    {
        /// <summary>
        /// Primary key
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Date for which quota usage is tracked (UTC date only)
        /// </summary>
        public DateOnly Date { get; set; }

        /// <summary>
        /// API service name (e.g., "YouTube Data API v3", "Google OAuth2")
        /// </summary>
        [Required]
        [StringLength(100)]
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// Number of API calls made on this date
        /// </summary>
        public int RequestsUsed { get; set; }

        /// <summary>
        /// Daily quota limit for this service
        /// </summary>
        public int QuotaLimit { get; set; }

        /// <summary>
        /// Cost units consumed (some APIs use quota units instead of simple request counts)
        /// </summary>
        public long CostUnitsUsed { get; set; }

        /// <summary>
        /// Daily cost unit limit
        /// </summary>
        public long CostUnitLimit { get; set; }

        /// <summary>
        /// When this record was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional notes or details about quota usage
        /// </summary>
        [StringLength(500)]
        public string? Notes { get; set; }

        /// <summary>
        /// Calculate the percentage of quota used
        /// </summary>
        public double RequestsUsagePercentage => QuotaLimit > 0 ? (double)RequestsUsed / QuotaLimit * 100 : 0;

        /// <summary>
        /// Calculate the percentage of cost units used
        /// </summary>
        public double CostUnitsUsagePercentage => CostUnitLimit > 0 ? (double)CostUnitsUsed / CostUnitLimit * 100 : 0;
    }
}