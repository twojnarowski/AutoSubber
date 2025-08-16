using AutoSubber.Data;
using AutoSubber.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AutoSubber.Tests.Services
{
    public class DiagnosticsServiceTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task GetSummaryStatsAsync_ReturnsExpectedStats()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var mockLogger = new Mock<ILogger<DiagnosticsService>>();
            var service = new DiagnosticsService(context, mockLogger.Object);

            // Add test data
            var testUser = new ApplicationUser { Id = "test-user-1", Email = "test@example.com" };
            context.Users.Add(testUser);

            context.Subscriptions.Add(new Subscription
            {
                UserId = testUser.Id,
                ChannelId = "test-channel-1",
                Title = "Test Channel",
                IsIncluded = true,
                PubSubSubscribed = true,
                PubSubLeaseExpiry = DateTime.UtcNow.AddDays(1)
            });

            context.ProcessedVideos.Add(new ProcessedVideo
            {
                UserId = testUser.Id,
                VideoId = "test-video-1",
                ChannelId = "test-channel-1",
                AddedToPlaylist = true,
                ProcessedAt = DateTime.UtcNow.AddHours(-1),
                Source = "Webhook"
            });

            await context.SaveChangesAsync();

            // Act
            var stats = await service.GetSummaryStatsAsync();

            // Assert
            Assert.NotNull(stats);
            Assert.True(stats.ContainsKey("ActiveSubscriptions"));
            Assert.True(stats.ContainsKey("PubSubSubscribed"));
            Assert.Equal(1, stats["ActiveSubscriptions"]);
            Assert.Equal(1, stats["PubSubSubscribed"]);
        }

        [Fact]
        public async Task GetQuotaUsageAsync_ReturnsCorrectRecords()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var mockLogger = new Mock<ILogger<DiagnosticsService>>();
            var service = new DiagnosticsService(context, mockLogger.Object);

            // Add test quota data
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterday = today.AddDays(-1);

            context.ApiQuotaUsages.AddRange(
                new ApiQuotaUsage
                {
                    Date = today,
                    ServiceName = "YouTube Data API v3",
                    RequestsUsed = 100,
                    QuotaLimit = 10000
                },
                new ApiQuotaUsage
                {
                    Date = yesterday,
                    ServiceName = "YouTube Data API v3",
                    RequestsUsed = 200,
                    QuotaLimit = 10000
                }
            );

            await context.SaveChangesAsync();

            // Act
            var quotaUsage = await service.GetQuotaUsageAsync(30);

            // Assert
            Assert.Equal(2, quotaUsage.Count);
            Assert.Equal(today, quotaUsage[0].Date); // Should be ordered by date descending
        }

        [Fact]
        public async Task GetFailedJobsAsync_ReturnsOnlyFailedJobs()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var mockLogger = new Mock<ILogger<DiagnosticsService>>();
            var service = new DiagnosticsService(context, mockLogger.Object);

            var testUser = new ApplicationUser { Id = "test-user-1", Email = "test@example.com" };
            context.Users.Add(testUser);

            // Add successful and failed jobs
            context.ProcessedVideos.AddRange(
                new ProcessedVideo
                {
                    UserId = testUser.Id,
                    VideoId = "success-video",
                    ChannelId = "test-channel",
                    AddedToPlaylist = true,
                    ProcessedAt = DateTime.UtcNow.AddHours(-1),
                    Source = "Webhook"
                },
                new ProcessedVideo
                {
                    UserId = testUser.Id,
                    VideoId = "failed-video",
                    ChannelId = "test-channel",
                    AddedToPlaylist = false,
                    ErrorMessage = "API quota exceeded",
                    ProcessedAt = DateTime.UtcNow.AddHours(-2),
                    Source = "Webhook"
                }
            );

            await context.SaveChangesAsync();

            // Act
            var failedJobs = await service.GetFailedJobsAsync(30);

            // Assert
            Assert.Single(failedJobs);
            Assert.Equal("failed-video", failedJobs[0].VideoId);
            Assert.False(failedJobs[0].AddedToPlaylist);
        }

        [Fact]
        public async Task UpdateQuotaUsageAsync_CreatesNewRecordIfNotExists()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var mockLogger = new Mock<ILogger<DiagnosticsService>>();
            var service = new DiagnosticsService(context, mockLogger.Object);

            // Act
            var result = await service.UpdateQuotaUsageAsync("YouTube Data API v3", 150, 10000, 300, 100000);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("YouTube Data API v3", result.ServiceName);
            Assert.Equal(150, result.RequestsUsed);
            Assert.Equal(10000, result.QuotaLimit);
            Assert.Equal(300, result.CostUnitsUsed);
            Assert.Equal(100000, result.CostUnitLimit);

            // Verify it was saved to database
            var saved = await context.ApiQuotaUsages.FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.Equal(result.Id, saved.Id);
        }

        [Fact]
        public async Task UpdateQuotaUsageAsync_UpdatesExistingRecord()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var mockLogger = new Mock<ILogger<DiagnosticsService>>();
            var service = new DiagnosticsService(context, mockLogger.Object);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var existing = new ApiQuotaUsage
            {
                Date = today,
                ServiceName = "YouTube Data API v3",
                RequestsUsed = 100,
                QuotaLimit = 10000
            };
            context.ApiQuotaUsages.Add(existing);
            await context.SaveChangesAsync();

            // Act
            var result = await service.UpdateQuotaUsageAsync("YouTube Data API v3", 250, 10000);

            // Assert
            Assert.Equal(existing.Id, result.Id);
            Assert.Equal(250, result.RequestsUsed);

            // Verify only one record exists
            var count = await context.ApiQuotaUsages.CountAsync();
            Assert.Equal(1, count);
        }
    }
}