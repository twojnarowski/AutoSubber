using AutoSubber.Services;

namespace AutoSubber.Services
{
    /// <summary>
    /// Background service that periodically checks and renews PubSubHubbub subscriptions
    /// </summary>
    public class PubSubRenewalBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PubSubRenewalBackgroundService> _logger;
        private readonly IConfiguration _configuration;
        
        // Check every 30 minutes for subscriptions needing renewal
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);

        public PubSubRenewalBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<PubSubRenewalBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PubSub renewal background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessSubscriptionRenewalsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during PubSub subscription renewal check");
                }

                try
                {
                    await Task.Delay(CheckInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("PubSub renewal background service stopped");
        }

        private async Task ProcessSubscriptionRenewalsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var pubSubService = scope.ServiceProvider.GetRequiredService<IPubSubSubscriptionService>();
            
            _logger.LogDebug("Checking for subscriptions needing PubSub attention");

            var subscriptionsNeedingAttention = await pubSubService.GetSubscriptionsNeedingAttentionAsync();

            if (subscriptionsNeedingAttention.Count == 0)
            {
                _logger.LogDebug("No subscriptions need PubSub attention");
                return;
            }

            _logger.LogInformation("Found {Count} subscriptions needing PubSub attention", subscriptionsNeedingAttention.Count);

            // Get the callback URL from configuration
            var baseUrl = _configuration["BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl))
            {
                _logger.LogWarning("BaseUrl not configured, cannot process PubSub subscriptions");
                return;
            }

            var callbackUrl = $"{baseUrl.TrimEnd('/')}/api/youtube/webhook";

            // Process each subscription
            var processedCount = 0;
            foreach (var subscription in subscriptionsNeedingAttention)
            {
                try
                {
                    var success = await pubSubService.ProcessSubscriptionAsync(subscription, callbackUrl);
                    if (success)
                    {
                        processedCount++;
                        _logger.LogInformation("Successfully processed PubSub subscription for channel {ChannelId}", subscription.ChannelId);
                    }
                    
                    // Add a small delay between requests to avoid overwhelming the PubSubHubbub service
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing PubSub subscription for channel {ChannelId}", subscription.ChannelId);
                }
            }

            _logger.LogInformation("Processed {ProcessedCount} of {TotalCount} PubSub subscriptions", 
                processedCount, subscriptionsNeedingAttention.Count);
        }
    }
}