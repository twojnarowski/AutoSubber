using AutoSubber.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoSubber.Services
{
    /// <summary>
    /// Implementation of diagnostics service providing admin insights
    /// </summary>
    public class DiagnosticsService : IDiagnosticsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DiagnosticsService> _logger;

        public DiagnosticsService(ApplicationDbContext context, ILogger<DiagnosticsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ApiQuotaUsage>> GetQuotaUsageAsync(int days = 30)
        {
            var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
            
            return await _context.ApiQuotaUsages
                .Where(q => q.Date >= cutoffDate)
                .OrderByDescending(q => q.Date)
                .ThenBy(q => q.ServiceName)
                .ToListAsync();
        }

        public async Task<List<ProcessedVideo>> GetFailedJobsAsync(int days = 30)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            
            return await _context.ProcessedVideos
                .Where(pv => !pv.AddedToPlaylist && pv.ProcessedAt >= cutoffDate)
                .OrderByDescending(pv => pv.ProcessedAt)
                .Include(pv => pv.User)
                .ToListAsync();
        }

        public async Task<List<Subscription>> GetWebhookHealthAsync()
        {
            return await _context.Subscriptions
                .OrderBy(s => s.Title)
                .ToListAsync();
        }

        public async Task<List<WebhookEvent>> GetUnprocessedWebhookEventsAsync(int hours = 24)
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-hours);
            
            return await _context.WebhookEvents
                .Where(we => !we.IsProcessed && we.ReceivedAt >= cutoffTime)
                .OrderByDescending(we => we.ReceivedAt)
                .ToListAsync();
        }

        public async Task<ApiQuotaUsage> UpdateQuotaUsageAsync(string serviceName, int requestsUsed, int quotaLimit, long costUnitsUsed = 0, long costUnitLimit = 0)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            
            var existingRecord = await _context.ApiQuotaUsages
                .FirstOrDefaultAsync(q => q.Date == today && q.ServiceName == serviceName);

            if (existingRecord != null)
            {
                existingRecord.RequestsUsed = requestsUsed;
                existingRecord.QuotaLimit = quotaLimit;
                existingRecord.CostUnitsUsed = costUnitsUsed;
                existingRecord.CostUnitLimit = costUnitLimit;
                existingRecord.LastUpdated = DateTime.UtcNow;
                
                _context.ApiQuotaUsages.Update(existingRecord);
            }
            else
            {
                existingRecord = new ApiQuotaUsage
                {
                    Date = today,
                    ServiceName = serviceName,
                    RequestsUsed = requestsUsed,
                    QuotaLimit = quotaLimit,
                    CostUnitsUsed = costUnitsUsed,
                    CostUnitLimit = costUnitLimit,
                    LastUpdated = DateTime.UtcNow
                };
                
                _context.ApiQuotaUsages.Add(existingRecord);
            }

            await _context.SaveChangesAsync();
            return existingRecord;
        }

        public async Task<Dictionary<string, object>> GetSummaryStatsAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var last24Hours = DateTime.UtcNow.AddHours(-24);
            var last7Days = DateTime.UtcNow.AddDays(-7);

            var stats = new Dictionary<string, object>();

            try
            {
                // Today's quota usage
                var todaysQuota = await _context.ApiQuotaUsages
                    .Where(q => q.Date == today)
                    .ToListAsync();
                stats["TodaysQuotaUsage"] = todaysQuota;

                // Active subscriptions count
                var activeSubscriptions = await _context.Subscriptions
                    .CountAsync(s => s.IsIncluded);
                stats["ActiveSubscriptions"] = activeSubscriptions;

                // PubSub subscribed count
                var pubSubSubscribed = await _context.Subscriptions
                    .CountAsync(s => s.PubSubSubscribed && s.PubSubLeaseExpiry > DateTime.UtcNow);
                stats["PubSubSubscribed"] = pubSubSubscribed;

                // Failed jobs in last 24 hours
                var recentFailedJobs = await _context.ProcessedVideos
                    .CountAsync(pv => !pv.AddedToPlaylist && pv.ProcessedAt >= last24Hours);
                stats["RecentFailedJobs"] = recentFailedJobs;

                // Unprocessed webhook events
                var unprocessedEvents = await _context.WebhookEvents
                    .CountAsync(we => !we.IsProcessed && we.ReceivedAt >= last24Hours);
                stats["UnprocessedEvents"] = unprocessedEvents;

                // Videos processed in last 7 days
                var recentlyProcessed = await _context.ProcessedVideos
                    .CountAsync(pv => pv.ProcessedAt >= last7Days);
                stats["RecentlyProcessedVideos"] = recentlyProcessed;

                // Successful processing rate (last 7 days)
                var successfulProcessed = await _context.ProcessedVideos
                    .CountAsync(pv => pv.AddedToPlaylist && pv.ProcessedAt >= last7Days);
                var successRate = recentlyProcessed > 0 ? (double)successfulProcessed / recentlyProcessed * 100 : 0;
                stats["SuccessRate"] = Math.Round(successRate, 1);

                // Webhook events received in last 24 hours
                var webhookEventsReceived = await _context.WebhookEvents
                    .CountAsync(we => we.ReceivedAt >= last24Hours);
                stats["WebhookEventsReceived"] = webhookEventsReceived;

                _logger.LogInformation("Successfully calculated summary statistics");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating summary statistics");
                stats["Error"] = "Failed to calculate some statistics";
            }

            return stats;
        }
    }
}