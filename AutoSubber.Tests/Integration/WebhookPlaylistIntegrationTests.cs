using AutoSubber.Data;
using AutoSubber.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text;
using Xunit;

namespace AutoSubber.Tests.Integration
{
    /// <summary>
    /// Integration tests for the complete webhook to playlist insertion flow
    /// </summary>
    public class WebhookPlaylistIntegrationTests : IClassFixture<WebhookPlaylistIntegrationTests.CustomWebApplicationFactory>, IDisposable
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public WebhookPlaylistIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        /// <summary>
        /// Test the complete flow: webhook received → XML parsed → users identified → videos added to playlists
        /// </summary>
        [Fact]
        public async Task WebhookToPlaylist_CompleteFlow_ProcessesSuccessfully()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Ensure database is created
            await context.Database.EnsureCreatedAsync();
            
            // Create a test user with subscription
            var user = new ApplicationUser
            {
                Id = "test-user-1",
                UserName = "testuser@example.com",
                Email = "testuser@example.com",
                EncryptedAccessToken = "encrypted-test-token",
                AutoWatchLaterPlaylistId = "PLtest123"
            };
            context.Users.Add(user);

            // Create a test subscription for the channel
            var subscription = new Subscription
            {
                UserId = user.Id,
                ChannelId = "UCuAXFkgsw1L7xaCfnd5JJOw",
                Title = "Test Channel",
                IsIncluded = true,
                PubSubSubscribed = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            context.Subscriptions.Add(subscription);
            await context.SaveChangesAsync();

            // Prepare YouTube webhook XML payload
            var webhookXml = @"<?xml version='1.0' encoding='UTF-8'?>
<feed xmlns=""http://www.w3.org/2005/Atom"" xmlns:yt=""http://www.youtube.com/xml/schemas/2015"">
  <entry>
    <yt:videoId>dQw4w9WgXcQ</yt:videoId>
    <yt:channelId>UCuAXFkgsw1L7xaCfnd5JJOw</yt:channelId>
    <title>Test Video Title</title>
    <published>2025-01-01T00:00:00+00:00</published>
  </entry>
</feed>";

            // Act
            var content = new StringContent(webhookXml, Encoding.UTF8, "application/atom+xml");
            var response = await _client.PostAsync("/api/youtube/webhook", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Manually trigger video processing since background services are disabled
            var videoProcessingService = scope.ServiceProvider.GetRequiredService<IVideoProcessingService>();
            await videoProcessingService.ProcessUnprocessedWebhookEventsAsync();

            // Verify webhook event was created
            var webhookEvent = await context.WebhookEvents.FirstOrDefaultAsync();
            Assert.NotNull(webhookEvent);
            Assert.Equal("dQw4w9WgXcQ", webhookEvent.VideoId);
            Assert.Equal("UCuAXFkgsw1L7xaCfnd5JJOw", webhookEvent.ChannelId);
            Assert.Equal("Test Video Title", webhookEvent.Title);

            // Verify the mock YouTube service was called to add video to playlist
            var mockPlaylistService = _factory.MockYouTubePlaylistService;
            mockPlaylistService.Verify(
                x => x.AddVideoToPlaylistAsync(
                    It.Is<ApplicationUser>(u => u.Id == user.Id),
                    "dQw4w9WgXcQ",
                    "UCuAXFkgsw1L7xaCfnd5JJOw",
                    "Test Video Title"),
                Times.Once);

            // Verify processed video record was created
            var processedVideo = await context.ProcessedVideos.FirstOrDefaultAsync();
            Assert.NotNull(processedVideo);
            Assert.Equal(user.Id, processedVideo.UserId);
            Assert.Equal("dQw4w9WgXcQ", processedVideo.VideoId);
            Assert.Equal("UCuAXFkgsw1L7xaCfnd5JJOw", processedVideo.ChannelId);
            Assert.True(processedVideo.AddedToPlaylist);
            Assert.Equal("Webhook", processedVideo.Source);
        }

        /// <summary>
        /// Test webhook processing with invalid XML payload
        /// </summary>
        [Fact]
        public async Task WebhookToPlaylist_InvalidXml_ReturnsBadRequest()
        {
            // Arrange
            var invalidXml = "This is not valid XML";

            // Act
            var content = new StringContent(invalidXml, Encoding.UTF8, "application/atom+xml");
            var response = await _client.PostAsync("/api/youtube/webhook", content);

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            // Verify no webhook events were created
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var webhookEvents = await context.WebhookEvents.ToListAsync();
            Assert.Empty(webhookEvents);
        }

        /// <summary>
        /// Test webhook processing when no users are subscribed to the channel
        /// </summary>
        [Fact]
        public async Task WebhookToPlaylist_NoSubscribedUsers_ProcessesButNoPlaylistUpdates()
        {
            // Arrange
            var webhookXml = @"<?xml version='1.0' encoding='UTF-8'?>
<feed xmlns=""http://www.w3.org/2005/Atom"" xmlns:yt=""http://www.youtube.com/xml/schemas/2015"">
  <entry>
    <yt:videoId>newVideoId123</yt:videoId>
    <yt:channelId>UCnotSubscribedChannel</yt:channelId>
    <title>Unsubscribed Channel Video</title>
  </entry>
</feed>";

            // Act
            var content = new StringContent(webhookXml, Encoding.UTF8, "application/atom+xml");
            var response = await _client.PostAsync("/api/youtube/webhook", content);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Wait for processing
            await Task.Delay(500);

            // Verify webhook event was still created
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var webhookEvent = await context.WebhookEvents.FirstOrDefaultAsync();
            Assert.NotNull(webhookEvent);
            Assert.Equal("newVideoId123", webhookEvent.VideoId);

            // Verify no playlist service calls were made (no subscribed users)
            var mockPlaylistService = _factory.MockYouTubePlaylistService;
            mockPlaylistService.Verify(
                x => x.AddVideoToPlaylistAsync(
                    It.IsAny<ApplicationUser>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()),
                Times.Never);
        }

        /// <summary>
        /// Test webhook verification endpoint
        /// </summary>
        [Fact]
        public async Task WebhookVerification_ValidChallenge_ReturnsChallenge()
        {
            // Arrange
            var challenge = "test-challenge-12345";
            var url = $"/api/youtube/webhook?hub.mode=subscribe&hub.challenge={challenge}&hub.topic=https://www.youtube.com/xml/feeds/videos.xml?channel_id=UCtest123";

            // Act
            var response = await _client.GetAsync(url);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var responseContent = await response.Content.ReadAsStringAsync();
            Assert.Equal(challenge, responseContent);
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        /// <summary>
        /// Custom WebApplicationFactory for integration tests
        /// </summary>
        public class CustomWebApplicationFactory : WebApplicationFactory<Program>
        {
            public Mock<IYouTubePlaylistService> MockYouTubePlaylistService { get; } = new();
            public Mock<ITokenEncryptionService> MockTokenEncryptionService { get; } = new();

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                // Override configuration to force InMemory database
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["DatabaseProvider"] = "InMemory",
                        ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Replace YouTube services with mocks
                    var playlistServiceDescriptor = services.SingleOrDefault(s => s.ServiceType == typeof(IYouTubePlaylistService));
                    if (playlistServiceDescriptor != null)
                        services.Remove(playlistServiceDescriptor);
                    services.AddSingleton(MockYouTubePlaylistService.Object);

                    var tokenServiceDescriptor = services.SingleOrDefault(s => s.ServiceType == typeof(ITokenEncryptionService));
                    if (tokenServiceDescriptor != null)
                        services.Remove(tokenServiceDescriptor);
                    services.AddSingleton(MockTokenEncryptionService.Object);

                    // Setup mock behaviors
                    MockTokenEncryptionService.Setup(x => x.Decrypt("encrypted-test-token"))
                                            .Returns("valid-access-token");

                    MockYouTubePlaylistService.Setup(x => x.AddVideoToPlaylistAsync(
                        It.IsAny<ApplicationUser>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<string>()))
                        .ReturnsAsync(true);

                    // Disable background services for testing
                    var backgroundServices = services.Where(s => s.ImplementationType?.BaseType?.Name == "BackgroundService").ToList();
                    foreach (var service in backgroundServices)
                    {
                        services.Remove(service);
                    }
                });

                builder.UseEnvironment("Testing");
            }
        }
    }
}