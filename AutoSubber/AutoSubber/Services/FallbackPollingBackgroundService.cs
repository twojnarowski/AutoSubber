using AutoSubber.Services;

namespace AutoSubber.Services
{
    /// <summary>
    /// Background service that periodically polls YouTube channels when PubSubHubbub fails or is disabled
    /// </summary>
    public class FallbackPollingBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FallbackPollingBackgroundService> _logger;
        private readonly IConfiguration _configuration;

        public FallbackPollingBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<FallbackPollingBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Fallback polling background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPollingCycleAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during fallback polling cycle");
                }

                try
                {
                    var pollingInterval = TimeSpan.FromHours(_configuration.GetValue<double>("YouTubePolling:IntervalHours", 1.0));
                    _logger.LogDebug("Waiting {Minutes} minutes until next polling cycle", pollingInterval.TotalMinutes);
                    await Task.Delay(pollingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Fallback polling background service stopped");
        }

        private async Task ProcessPollingCycleAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var pollingService = scope.ServiceProvider.GetRequiredService<IYouTubePollingService>();
            
            _logger.LogDebug("Starting fallback polling cycle");

            var subscriptionsNeedingPolling = await pollingService.GetSubscriptionsNeedingPollingAsync();

            if (subscriptionsNeedingPolling.Count == 0)
            {
                _logger.LogDebug("No subscriptions need fallback polling");
                return;
            }

            _logger.LogInformation("Found {Count} subscriptions needing fallback polling", subscriptionsNeedingPolling.Count);

            var totalVideosFound = 0;
            var successfulPolls = 0;

            // Process each subscription
            foreach (var subscription in subscriptionsNeedingPolling)
            {
                try
                {
                    var newVideosCount = await pollingService.PollChannelForNewVideosAsync(subscription);
                    totalVideosFound += newVideosCount;
                    successfulPolls++;
                    
                    if (newVideosCount > 0)
                    {
                        _logger.LogInformation("Found {Count} new videos for channel {ChannelId} (Title: {Title})", 
                            newVideosCount, subscription.ChannelId, subscription.Title);
                    }
                    
                    // Add a small delay between requests to avoid overwhelming the YouTube API
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error polling channel {ChannelId} (Title: {Title})", 
                        subscription.ChannelId, subscription.Title);
                }
            }

            _logger.LogInformation("Completed fallback polling cycle: {SuccessfulPolls}/{TotalPolls} channels polled successfully, found {TotalVideos} new videos", 
                successfulPolls, subscriptionsNeedingPolling.Count, totalVideosFound);
        }
    }
}