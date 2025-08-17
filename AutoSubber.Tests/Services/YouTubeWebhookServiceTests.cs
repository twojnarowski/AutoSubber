using AutoSubber.Data;
using AutoSubber.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AutoSubber.Tests.Services
{
    public class YouTubeWebhookServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly YouTubeWebhookService _webhookService;
        private readonly Mock<ILogger<YouTubeWebhookService>> _mockLogger;

        public YouTubeWebhookServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockLogger = new Mock<ILogger<YouTubeWebhookService>>();
            _webhookService = new YouTubeWebhookService(_context, _mockLogger.Object);
        }

        [Fact]
        public async Task ProcessWebhookAsync_WithValidXml_ReturnsTrue()
        {
            // Arrange
            var validXml = @"<?xml version='1.0' encoding='UTF-8'?>
<feed xmlns=""http://www.w3.org/2005/Atom"" xmlns:yt=""http://www.youtube.com/xml/schemas/2015"">
  <entry>
    <yt:videoId>dQw4w9WgXcQ</yt:videoId>
    <yt:channelId>UCuAXFkgsw1L7xaCfnd5JJOw</yt:channelId>
    <title>Test Video Title</title>
  </entry>
</feed>";

            // Act
            var result = await _webhookService.ProcessWebhookAsync(validXml);

            // Assert
            Assert.True(result);

            var webhookEvent = await _context.WebhookEvents.FirstOrDefaultAsync();
            Assert.NotNull(webhookEvent);
            Assert.Equal("dQw4w9WgXcQ", webhookEvent.VideoId);
            Assert.Equal("UCuAXFkgsw1L7xaCfnd5JJOw", webhookEvent.ChannelId);
            Assert.Equal("Test Video Title", webhookEvent.Title);
            Assert.Equal(validXml, webhookEvent.RawPayload);
            Assert.False(webhookEvent.IsProcessed);
            Assert.True(webhookEvent.ReceivedAt <= DateTime.UtcNow);
        }

        [Fact]
        public async Task ProcessWebhookAsync_WithInvalidXml_ReturnsFalse()
        {
            // Arrange
            var invalidXml = "This is not valid XML";

            // Act
            var result = await _webhookService.ProcessWebhookAsync(invalidXml);

            // Assert
            Assert.False(result);

            var webhookEvent = await _context.WebhookEvents.FirstOrDefaultAsync();
            Assert.Null(webhookEvent);
        }

        [Fact]
        public async Task ProcessWebhookAsync_WithMissingVideoId_ReturnsFalse()
        {
            // Arrange
            var xmlWithoutVideoId = @"<?xml version='1.0' encoding='UTF-8'?>
<feed xmlns=""http://www.w3.org/2005/Atom"" xmlns:yt=""http://www.youtube.com/xml/schemas/2015"">
  <entry>
    <yt:channelId>UCuAXFkgsw1L7xaCfnd5JJOw</yt:channelId>
    <title>Test Video Title</title>
  </entry>
</feed>";

            // Act
            var result = await _webhookService.ProcessWebhookAsync(xmlWithoutVideoId);

            // Assert
            Assert.False(result);

            var webhookEvent = await _context.WebhookEvents.FirstOrDefaultAsync();
            Assert.Null(webhookEvent);
        }

        [Fact]
        public async Task ProcessWebhookAsync_WithMissingChannelId_ReturnsFalse()
        {
            // Arrange
            var xmlWithoutChannelId = @"<?xml version='1.0' encoding='UTF-8'?>
<feed xmlns=""http://www.w3.org/2005/Atom"" xmlns:yt=""http://www.youtube.com/xml/schemas/2015"">
  <entry>
    <yt:videoId>dQw4w9WgXcQ</yt:videoId>
    <title>Test Video Title</title>
  </entry>
</feed>";

            // Act
            var result = await _webhookService.ProcessWebhookAsync(xmlWithoutChannelId);

            // Assert
            Assert.False(result);

            var webhookEvent = await _context.WebhookEvents.FirstOrDefaultAsync();
            Assert.Null(webhookEvent);
        }

        [Fact]
        public async Task ProcessWebhookAsync_WithoutTitle_StillProcesses()
        {
            // Arrange
            var xmlWithoutTitle = @"<?xml version='1.0' encoding='UTF-8'?>
<feed xmlns=""http://www.w3.org/2005/Atom"" xmlns:yt=""http://www.youtube.com/xml/schemas/2015"">
  <entry>
    <yt:videoId>dQw4w9WgXcQ</yt:videoId>
    <yt:channelId>UCuAXFkgsw1L7xaCfnd5JJOw</yt:channelId>
  </entry>
</feed>";

            // Act
            var result = await _webhookService.ProcessWebhookAsync(xmlWithoutTitle);

            // Assert
            Assert.True(result);

            var webhookEvent = await _context.WebhookEvents.FirstOrDefaultAsync();
            Assert.NotNull(webhookEvent);
            Assert.Equal("dQw4w9WgXcQ", webhookEvent.VideoId);
            Assert.Equal("UCuAXFkgsw1L7xaCfnd5JJOw", webhookEvent.ChannelId);
            Assert.Null(webhookEvent.Title);
        }

        [Fact]
        public async Task GetUnprocessedEventsAsync_ReturnsOnlyUnprocessedEvents()
        {
            // Arrange
            var processedEvent = new WebhookEvent
            {
                VideoId = "processed-video",
                ChannelId = "channel1",
                Title = "Processed Video",
                RawPayload = "xml1",
                ReceivedAt = DateTime.UtcNow.AddMinutes(-10),
                IsProcessed = true,
                ProcessedAt = DateTime.UtcNow.AddMinutes(-5)
            };

            var unprocessedEvent1 = new WebhookEvent
            {
                VideoId = "unprocessed-video-1",
                ChannelId = "channel2",
                Title = "Unprocessed Video 1",
                RawPayload = "xml2",
                ReceivedAt = DateTime.UtcNow.AddMinutes(-8),
                IsProcessed = false
            };

            var unprocessedEvent2 = new WebhookEvent
            {
                VideoId = "unprocessed-video-2",
                ChannelId = "channel3",
                Title = "Unprocessed Video 2",
                RawPayload = "xml3",
                ReceivedAt = DateTime.UtcNow.AddMinutes(-5),
                IsProcessed = false
            };

            _context.WebhookEvents.AddRange(processedEvent, unprocessedEvent1, unprocessedEvent2);
            await _context.SaveChangesAsync();

            // Act
            var unprocessedEvents = await _webhookService.GetUnprocessedEventsAsync();

            // Assert
            Assert.Equal(2, unprocessedEvents.Count);
            Assert.All(unprocessedEvents, e => Assert.False(e.IsProcessed));
            
            // Should be ordered by ReceivedAt
            Assert.Equal("unprocessed-video-1", unprocessedEvents[0].VideoId);
            Assert.Equal("unprocessed-video-2", unprocessedEvents[1].VideoId);
        }

        [Fact]
        public async Task GetUnprocessedEventsAsync_WhenNoUnprocessedEvents_ReturnsEmptyList()
        {
            // Arrange
            var processedEvent = new WebhookEvent
            {
                VideoId = "processed-video",
                ChannelId = "channel1",
                Title = "Processed Video",
                RawPayload = "xml1",
                ReceivedAt = DateTime.UtcNow.AddMinutes(-10),
                IsProcessed = true,
                ProcessedAt = DateTime.UtcNow.AddMinutes(-5)
            };

            _context.WebhookEvents.Add(processedEvent);
            await _context.SaveChangesAsync();

            // Act
            var unprocessedEvents = await _webhookService.GetUnprocessedEventsAsync();

            // Assert
            Assert.Empty(unprocessedEvents);
        }

        [Fact]
        public async Task MarkEventProcessedAsync_WithValidEventId_ReturnsTrue()
        {
            // Arrange
            var webhookEvent = new WebhookEvent
            {
                VideoId = "test-video",
                ChannelId = "test-channel",
                Title = "Test Video",
                RawPayload = "xml",
                ReceivedAt = DateTime.UtcNow.AddMinutes(-10),
                IsProcessed = false
            };

            _context.WebhookEvents.Add(webhookEvent);
            await _context.SaveChangesAsync();

            var eventId = webhookEvent.Id;

            // Act
            var result = await _webhookService.MarkEventProcessedAsync(eventId);

            // Assert
            Assert.True(result);

            var updatedEvent = await _context.WebhookEvents.FindAsync(eventId);
            Assert.NotNull(updatedEvent);
            Assert.True(updatedEvent.IsProcessed);
            Assert.NotNull(updatedEvent.ProcessedAt);
            Assert.True(updatedEvent.ProcessedAt <= DateTime.UtcNow);
        }

        [Fact]
        public async Task MarkEventProcessedAsync_WithInvalidEventId_ReturnsFalse()
        {
            // Act
            var result = await _webhookService.MarkEventProcessedAsync(999);

            // Assert
            Assert.False(result);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}