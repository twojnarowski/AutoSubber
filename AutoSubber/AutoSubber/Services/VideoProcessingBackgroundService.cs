using AutoSubber.Services;

namespace AutoSubber.Services
{
    /// <summary>
    /// Background service that processes discovered videos and adds them to user playlists
    /// </summary>
    public class VideoProcessingBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<VideoProcessingBackgroundService> _logger;
        private readonly IConfiguration _configuration;

        public VideoProcessingBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<VideoProcessingBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Video processing background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessVideosAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during video processing cycle");
                }

                try
                {
                    var processingInterval = TimeSpan.FromMinutes(_configuration.GetValue<double>("VideoProcessing:IntervalMinutes", 5.0));
                    _logger.LogDebug("Waiting {Minutes} minutes until next video processing cycle", processingInterval.TotalMinutes);
                    await Task.Delay(processingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("Video processing background service stopped");
        }

        private async Task ProcessVideosAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var videoProcessingService = scope.ServiceProvider.GetRequiredService<IVideoProcessingService>();

            _logger.LogDebug("Starting video processing cycle");

            try
            {
                // Process unprocessed webhook events
                var webhookVideosProcessed = await videoProcessingService.ProcessUnprocessedWebhookEventsAsync();

                if (webhookVideosProcessed > 0)
                {
                    _logger.LogInformation("Processed {Count} videos from webhook events", webhookVideosProcessed);
                }
                else
                {
                    _logger.LogDebug("No webhook videos to process");
                }

                _logger.LogDebug("Completed video processing cycle: {WebhookVideos} webhook videos processed",
                    webhookVideosProcessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during video processing cycle");
            }
        }
    }
}