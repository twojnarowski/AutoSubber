using AutoSubber.Data;
using AutoSubber.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;

namespace AutoSubber.Tests.Services
{
    public class PubSubSubscriptionServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<PubSubSubscriptionService>> _mockLogger;
        private readonly PubSubSubscriptionService _pubSubService;

        public PubSubSubscriptionServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _mockLogger = new Mock<ILogger<PubSubSubscriptionService>>();
            _pubSubService = new PubSubSubscriptionService(_httpClient, _context, _mockLogger.Object);
        }

        [Fact]
        public async Task SubscribeToChannelAsync_WithValidRequest_ReturnsTrue()
        {
            // Arrange
            var channelId = "UC123456789";
            var callbackUrl = "https://example.com/webhook";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var result = await _pubSubService.SubscribeToChannelAsync(channelId, callbackUrl);

            // Assert
            Assert.True(result);

            // Verify the HTTP request was made correctly
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString() == "https://pubsubhubbub.appspot.com/subscribe"),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task SubscribeToChannelAsync_WithHttpError_ReturnsFalse()
        {
            // Arrange
            var channelId = "UC123456789";
            var callbackUrl = "https://example.com/webhook";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

            // Act
            var result = await _pubSubService.SubscribeToChannelAsync(channelId, callbackUrl);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task SubscribeToChannelAsync_WithHttpException_ReturnsFalse()
        {
            // Arrange
            var channelId = "UC123456789";
            var callbackUrl = "https://example.com/webhook";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));

            // Act
            var result = await _pubSubService.SubscribeToChannelAsync(channelId, callbackUrl);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task UnsubscribeFromChannelAsync_WithValidRequest_ReturnsTrue()
        {
            // Arrange
            var channelId = "UC123456789";
            var callbackUrl = "https://example.com/webhook";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var result = await _pubSubService.UnsubscribeFromChannelAsync(channelId, callbackUrl);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task UnsubscribeFromChannelAsync_WithHttpError_ReturnsFalse()
        {
            // Arrange
            var channelId = "UC123456789";
            var callbackUrl = "https://example.com/webhook";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

            // Act
            var result = await _pubSubService.UnsubscribeFromChannelAsync(channelId, callbackUrl);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetSubscriptionsNeedingAttentionAsync_ReturnsUnsubscribedSubscriptions()
        {
            // Arrange
            var subscription1 = new Subscription
            {
                ChannelId = "UC111",
                Title = "Channel 1",
                IsIncluded = true,
                PubSubSubscribed = false
            };

            var subscription2 = new Subscription
            {
                ChannelId = "UC222",
                Title = "Channel 2",
                IsIncluded = true,
                PubSubSubscribed = true,
                PubSubLeaseExpiry = DateTime.UtcNow.AddDays(2) // Not expiring soon
            };

            _context.Subscriptions.AddRange(subscription1, subscription2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _pubSubService.GetSubscriptionsNeedingAttentionAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal("UC111", result[0].ChannelId);
        }

        [Fact]
        public async Task GetSubscriptionsNeedingAttentionAsync_ReturnsExpiringSubscriptions()
        {
            // Arrange
            var subscription1 = new Subscription
            {
                ChannelId = "UC111",
                Title = "Channel 1",
                IsIncluded = true,
                PubSubSubscribed = true,
                PubSubLeaseExpiry = DateTime.UtcNow.AddHours(12) // Expires within 24 hours
            };

            var subscription2 = new Subscription
            {
                ChannelId = "UC222",
                Title = "Channel 2",
                IsIncluded = true,
                PubSubSubscribed = true,
                PubSubLeaseExpiry = DateTime.UtcNow.AddDays(2) // Expires in 2 days
            };

            _context.Subscriptions.AddRange(subscription1, subscription2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _pubSubService.GetSubscriptionsNeedingAttentionAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal("UC111", result[0].ChannelId);
        }

        [Fact]
        public async Task GetSubscriptionsNeedingAttentionAsync_ReturnsRetryableFailedSubscriptions()
        {
            // Arrange
            var subscription = new Subscription
            {
                ChannelId = "UC111",
                Title = "Channel 1",
                IsIncluded = true,
                PubSubSubscribed = false,
                PubSubSubscriptionAttempts = 2,
                PubSubLastAttempt = DateTime.UtcNow.AddMinutes(-5) // Should be ready for retry (2^2 = 4 minutes)
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // Act
            var result = await _pubSubService.GetSubscriptionsNeedingAttentionAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal("UC111", result[0].ChannelId);
        }

        [Fact]
        public async Task GetSubscriptionsNeedingAttentionAsync_ExcludesMaxAttemptReachedSubscriptions()
        {
            // Arrange
            var subscription = new Subscription
            {
                ChannelId = "UC111",
                Title = "Channel 1",
                IsIncluded = true,
                PubSubSubscribed = true, // Already subscribed
                PubSubSubscriptionAttempts = 5, // Max attempts reached
                PubSubLastAttempt = DateTime.UtcNow.AddHours(-1),
                PubSubLeaseExpiry = DateTime.UtcNow.AddDays(2) // Not expiring soon
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // Act
            var result = await _pubSubService.GetSubscriptionsNeedingAttentionAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetSubscriptionsNeedingAttentionAsync_ExcludesNotIncludedSubscriptions()
        {
            // Arrange
            var subscription = new Subscription
            {
                ChannelId = "UC111",
                Title = "Channel 1",
                IsIncluded = false, // Not included
                PubSubSubscribed = false
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            // Act
            var result = await _pubSubService.GetSubscriptionsNeedingAttentionAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task ProcessSubscriptionAsync_WithSuccessfulSubscription_ReturnsTrue()
        {
            // Arrange
            var subscription = new Subscription
            {
                Id = 1,
                ChannelId = "UC111",
                Title = "Channel 1",
                IsIncluded = true,
                PubSubSubscribed = false,
                PubSubSubscriptionAttempts = 0
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            var callbackUrl = "https://example.com/webhook";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            // Act
            var result = await _pubSubService.ProcessSubscriptionAsync(subscription, callbackUrl);

            // Assert
            Assert.True(result);

            // Verify subscription was updated
            var updatedSubscription = await _context.Subscriptions.FindAsync(1);
            Assert.NotNull(updatedSubscription);
            Assert.True(updatedSubscription.PubSubSubscribed);
            Assert.NotNull(updatedSubscription.PubSubLeaseExpiry);
            Assert.Equal(0, updatedSubscription.PubSubSubscriptionAttempts);
        }

        [Fact]
        public async Task ProcessSubscriptionAsync_WithFailedSubscription_ReturnsFalse()
        {
            // Arrange
            var subscription = new Subscription
            {
                Id = 1,
                ChannelId = "UC111",
                Title = "Channel 1",
                IsIncluded = true,
                PubSubSubscribed = false,
                PubSubSubscriptionAttempts = 0
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            var callbackUrl = "https://example.com/webhook";

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

            // Act
            var result = await _pubSubService.ProcessSubscriptionAsync(subscription, callbackUrl);

            // Assert
            Assert.False(result);

            // Verify attempt was recorded
            var updatedSubscription = await _context.Subscriptions.FindAsync(1);
            Assert.NotNull(updatedSubscription);
            Assert.False(updatedSubscription.PubSubSubscribed);
            Assert.Equal(1, updatedSubscription.PubSubSubscriptionAttempts);
            Assert.NotNull(updatedSubscription.PubSubLastAttempt);
        }

        [Fact]
        public async Task ProcessSubscriptionAsync_WithTooEarlyRetry_ReturnsFalse()
        {
            // Arrange
            var subscription = new Subscription
            {
                Id = 1,
                ChannelId = "UC111",
                Title = "Channel 1",
                IsIncluded = true,
                PubSubSubscribed = false,
                PubSubSubscriptionAttempts = 2,
                PubSubLastAttempt = DateTime.UtcNow.AddMinutes(-2) // Too early for retry (needs 4 minutes)
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            var callbackUrl = "https://example.com/webhook";

            // Act
            var result = await _pubSubService.ProcessSubscriptionAsync(subscription, callbackUrl);

            // Assert
            Assert.False(result);

            // Verify attempt count wasn't incremented
            var updatedSubscription = await _context.Subscriptions.FindAsync(1);
            Assert.NotNull(updatedSubscription);
            Assert.Equal(2, updatedSubscription.PubSubSubscriptionAttempts);
        }

        [Fact]
        public async Task UpdateSubscriptionStatusAsync_WithValidId_ReturnsTrue()
        {
            // Arrange
            var subscription = new Subscription
            {
                Id = 1,
                ChannelId = "UC111",
                Title = "Channel 1",
                IsIncluded = true,
                PubSubSubscribed = false
            };

            _context.Subscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            var leaseExpiry = DateTime.UtcNow.AddDays(5);

            // Act
            var result = await _pubSubService.UpdateSubscriptionStatusAsync(1, true, leaseExpiry);

            // Assert
            Assert.True(result);

            var updatedSubscription = await _context.Subscriptions.FindAsync(1);
            Assert.NotNull(updatedSubscription);
            Assert.True(updatedSubscription.PubSubSubscribed);
            Assert.Equal(leaseExpiry, updatedSubscription.PubSubLeaseExpiry);
            Assert.Equal(0, updatedSubscription.PubSubSubscriptionAttempts);
        }

        [Fact]
        public async Task UpdateSubscriptionStatusAsync_WithInvalidId_ReturnsFalse()
        {
            // Act
            var result = await _pubSubService.UpdateSubscriptionStatusAsync(999, true);

            // Assert
            Assert.False(result);
        }

        public void Dispose()
        {
            _context.Dispose();
            _httpClient.Dispose();
        }
    }
}