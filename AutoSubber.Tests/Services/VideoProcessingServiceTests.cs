using AutoSubber.Data;
using AutoSubber.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AutoSubber.Tests.Services
{
    public class VideoProcessingServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly VideoProcessingService _videoProcessingService;
        private readonly TestYouTubePlaylistService _playlistService;
        private readonly TestYouTubeWebhookService _webhookService;

        public VideoProcessingServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _playlistService = new TestYouTubePlaylistService();
            _webhookService = new TestYouTubeWebhookService(_context);
            
            var logger = new TestLogger<VideoProcessingService>();
            _videoProcessingService = new VideoProcessingService(_context, logger, _playlistService, _webhookService);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        [Fact]
        public async Task IsVideoAlreadyProcessedAsync_WhenVideoNotProcessed_ReturnsFalse()
        {
            // Arrange
            var userId = "user1";
            var videoId = "video1";

            // Act
            var result = await _videoProcessingService.IsVideoAlreadyProcessedAsync(userId, videoId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsVideoAlreadyProcessedAsync_WhenVideoAlreadyProcessed_ReturnsTrue()
        {
            // Arrange
            var userId = "user1";
            var videoId = "video1";
            
            var processedVideo = new ProcessedVideo
            {
                UserId = userId,
                VideoId = videoId,
                ChannelId = "channel1",
                Source = "Test",
                AddedToPlaylist = true
            };
            
            _context.ProcessedVideos.Add(processedVideo);
            await _context.SaveChangesAsync();

            // Act
            var result = await _videoProcessingService.IsVideoAlreadyProcessedAsync(userId, videoId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task RecordProcessedVideoAsync_WhenSuccessful_ReturnsTrue()
        {
            // Arrange
            var userId = "user1";
            var videoId = "video1";
            var channelId = "channel1";
            var title = "Test Video";
            var source = "Test";

            // Act
            var result = await _videoProcessingService.RecordProcessedVideoAsync(
                userId, videoId, channelId, title, source, true);

            // Assert
            Assert.True(result);
            
            var processedVideo = await _context.ProcessedVideos
                .FirstOrDefaultAsync(pv => pv.UserId == userId && pv.VideoId == videoId);
            
            Assert.NotNull(processedVideo);
            Assert.Equal(title, processedVideo.Title);
            Assert.Equal(source, processedVideo.Source);
            Assert.True(processedVideo.AddedToPlaylist);
        }

        [Fact]
        public async Task ProcessVideoDiscoveryAsync_WhenNoSubscribedUsers_ReturnsZero()
        {
            // Act
            var result = await _videoProcessingService.ProcessVideoDiscoveryAsync("video1", "channel1", "Test Video", "Test");

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task ProcessVideoDiscoveryAsync_WhenUserSubscribed_ProcessesVideo()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                UserName = "testuser",
                AutoWatchLaterPlaylistId = "playlist1",
                EncryptedAccessToken = "token",
                AutomationDisabled = false
            };

            var subscription = new Subscription
            {
                UserId = user.Id,
                ChannelId = "channel1",
                Title = "Test Channel",
                IsIncluded = true,
                User = user
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // Act
            var result = await _videoProcessingService.ProcessVideoDiscoveryAsync("video1", "channel1", "Test Video", "Test");

            // Assert
            Assert.Equal(1, result);
            
            var processedVideo = await _context.ProcessedVideos
                .FirstOrDefaultAsync(pv => pv.UserId == user.Id && pv.VideoId == "video1");
            
            Assert.NotNull(processedVideo);
            Assert.Equal("Test Video", processedVideo.Title);
            Assert.Equal("Test", processedVideo.Source);
            Assert.True(processedVideo.AddedToPlaylist);
        }
    }

    // Test implementations
    internal class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    internal class TestYouTubePlaylistService : IYouTubePlaylistService
    {
        public Task<string?> CreateAutoWatchLaterPlaylistAsync(ApplicationUser user)
        {
            return Task.FromResult<string?>("playlist_id");
        }

        public Task<bool> AddVideoToPlaylistAsync(ApplicationUser user, string videoId, string channelId, string? videoTitle = null)
        {
            // Always succeed for testing
            return Task.FromResult(true);
        }
    }

    internal class TestYouTubeWebhookService : IYouTubeWebhookService
    {
        private readonly ApplicationDbContext _context;

        public TestYouTubeWebhookService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<bool> ProcessWebhookAsync(string xmlPayload)
        {
            throw new NotImplementedException();
        }

        public async Task<List<WebhookEvent>> GetUnprocessedEventsAsync()
        {
            return await _context.WebhookEvents
                .Where(e => !e.IsProcessed)
                .OrderBy(e => e.ReceivedAt)
                .ToListAsync();
        }

        public async Task<bool> MarkEventProcessedAsync(int eventId)
        {
            var webhookEvent = await _context.WebhookEvents.FindAsync(eventId);
            if (webhookEvent == null) return false;

            webhookEvent.IsProcessed = true;
            webhookEvent.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}