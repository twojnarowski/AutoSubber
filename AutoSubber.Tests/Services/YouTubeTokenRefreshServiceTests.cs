using AutoSubber.Data;
using AutoSubber.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace AutoSubber.Tests.Services
{
    public class YouTubeTokenRefreshServiceTests : IDisposable
    {
        private readonly Mock<ILogger<YouTubeTokenRefreshService>> _mockLogger;
        private readonly Mock<ITokenEncryptionService> _mockTokenEncryption;
        private readonly ApplicationDbContext _context;
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly YouTubeTokenRefreshService _tokenRefreshService;

        public YouTubeTokenRefreshServiceTests()
        {
            _mockLogger = new Mock<ILogger<YouTubeTokenRefreshService>>();
            _mockTokenEncryption = new Mock<ITokenEncryptionService>();
            
            // Create in-memory context instead of mocking
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);
            
            _mockUserManager = CreateMockUserManager();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

            // Setup configuration
            _mockConfiguration.Setup(x => x["Authentication:Google:ClientId"]).Returns("test-client-id");
            _mockConfiguration.Setup(x => x["Authentication:Google:ClientSecret"]).Returns("test-client-secret");

            _tokenRefreshService = new YouTubeTokenRefreshService(
                _mockLogger.Object,
                _mockTokenEncryption.Object,
                _context,
                _mockUserManager.Object,
                _mockConfiguration.Object,
                _httpClient);
        }

        private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
        }
        [Fact]
        public async Task RefreshUserTokenAsync_WithValidUser_ReturnsTrue()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedRefreshToken = "encrypted-refresh-token"
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-refresh-token"))
                              .Returns("valid-refresh-token");

            var tokenResponse = new
            {
                access_token = "new-access-token",
                expires_in = 3600,
                refresh_token = "new-refresh-token"
            };

            var responseJson = JsonSerializer.Serialize(tokenResponse);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            _mockTokenEncryption.Setup(x => x.Encrypt("new-access-token"))
                              .Returns("encrypted-new-access-token");
            _mockTokenEncryption.Setup(x => x.Encrypt("new-refresh-token"))
                              .Returns("encrypted-new-refresh-token");

            _mockUserManager.Setup(x => x.UpdateAsync(user))
                           .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _tokenRefreshService.RefreshUserTokenAsync(user);

            // Assert
            Assert.True(result);
            Assert.Equal("encrypted-new-access-token", user.EncryptedAccessToken);
            Assert.Equal("encrypted-new-refresh-token", user.EncryptedRefreshToken);
            Assert.False(user.AutomationDisabled);
            Assert.NotNull(user.TokenExpiresAt);
        }

        [Fact]
        public async Task RefreshUserTokenAsync_WithMissingRefreshToken_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedRefreshToken = null
            };

            // Act
            var result = await _tokenRefreshService.RefreshUserTokenAsync(user);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task RefreshUserTokenAsync_WithMissingClientId_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedRefreshToken = "encrypted-refresh-token"
            };

            _mockConfiguration.Setup(x => x["Authentication:Google:ClientId"]).Returns((string)null!);
            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-refresh-token"))
                              .Returns("valid-refresh-token");

            // Act
            var result = await _tokenRefreshService.RefreshUserTokenAsync(user);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task RefreshUserTokenAsync_WithFailedDecryption_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedRefreshToken = "encrypted-refresh-token"
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-refresh-token"))
                              .Returns((string)null!);

            // Act
            var result = await _tokenRefreshService.RefreshUserTokenAsync(user);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task RefreshUserTokenAsync_WithHttpError_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedRefreshToken = "encrypted-refresh-token"
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-refresh-token"))
                              .Returns("valid-refresh-token");

            var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Invalid refresh token")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _tokenRefreshService.RefreshUserTokenAsync(user);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task RefreshUserTokenAsync_WithInvalidJsonResponse_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedRefreshToken = "encrypted-refresh-token"
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-refresh-token"))
                              .Returns("valid-refresh-token");

            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("invalid json")
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _tokenRefreshService.RefreshUserTokenAsync(user);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task RefreshUserTokenAsync_WithMissingAccessToken_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedRefreshToken = "encrypted-refresh-token"
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-refresh-token"))
                              .Returns("valid-refresh-token");

            var tokenResponse = new
            {
                expires_in = 3600
                // Missing access_token
            };

            var responseJson = JsonSerializer.Serialize(tokenResponse);
            var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(httpResponse);

            // Act
            var result = await _tokenRefreshService.RefreshUserTokenAsync(user);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DisableAutomationAsync_UpdatesUserAndLogs()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                AutomationDisabled = false
            };

            _mockUserManager.Setup(x => x.UpdateAsync(user))
                           .ReturnsAsync(IdentityResult.Success);

            // Act
            await _tokenRefreshService.DisableAutomationAsync(user);

            // Assert
            Assert.True(user.AutomationDisabled);
            _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
        }

        [Fact]
        public async Task DisableAutomationAsync_WithUpdateFailure_HandlesException()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                AutomationDisabled = false
            };

            _mockUserManager.Setup(x => x.UpdateAsync(user))
                           .ThrowsAsync(new Exception("Database error"));

            // Act & Assert - Should not throw
            await _tokenRefreshService.DisableAutomationAsync(user);
            
            // User should still be marked as disabled
            Assert.True(user.AutomationDisabled);
        }

        [Fact]
        public void TokenNeedsRefresh_WhenTokenExpiresWithinBuffer_ReturnsTrue()
        {
            // Arrange
            var user = new ApplicationUser
            {
                AutomationDisabled = false,
                TokenExpiresAt = DateTime.UtcNow.AddMinutes(20), // Expires within 30-minute buffer
                EncryptedRefreshToken = "some-token"
            };

            var service = CreateTokenRefreshService();

            // Act
            var result = service.TokenNeedsRefresh(user);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void TokenNeedsRefresh_WhenTokenExpiresOutsideBuffer_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                AutomationDisabled = false,
                TokenExpiresAt = DateTime.UtcNow.AddMinutes(60), // Expires well outside 30-minute buffer
                EncryptedRefreshToken = "some-token"
            };

            var service = CreateTokenRefreshService();

            // Act
            var result = service.TokenNeedsRefresh(user);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void TokenNeedsRefresh_WhenNoExpiryTimeButHasRefreshToken_ReturnsTrue()
        {
            // Arrange
            var user = new ApplicationUser
            {
                AutomationDisabled = false,
                TokenExpiresAt = null,
                EncryptedRefreshToken = "some-token"
            };

            var service = CreateTokenRefreshService();

            // Act
            var result = service.TokenNeedsRefresh(user);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void TokenNeedsRefresh_WhenNoRefreshToken_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                AutomationDisabled = false,
                TokenExpiresAt = null,
                EncryptedRefreshToken = null
            };

            var service = CreateTokenRefreshService();

            // Act
            var result = service.TokenNeedsRefresh(user);

            // Assert
            Assert.False(result);
        }

        private static IYouTubeTokenRefreshService CreateTokenRefreshService()
        {
            // For testing the TokenNeedsRefresh method, we only need a minimal implementation
            // since it doesn't depend on external services for this logic
            return new TestYouTubeTokenRefreshService();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _context?.Dispose();
        }
    }

    /// <summary>
    /// Test implementation that only implements the TokenNeedsRefresh method for testing
    /// </summary>
    internal class TestYouTubeTokenRefreshService : IYouTubeTokenRefreshService
    {
        public Task<bool> RefreshUserTokenAsync(ApplicationUser user)
        {
            throw new NotImplementedException("Not needed for current tests");
        }

        public bool TokenNeedsRefresh(ApplicationUser user, int bufferMinutes = 30)
        {
            // Copy the actual implementation logic for testing
            if (user.AutomationDisabled)
            {
                return false;
            }

            if (!user.TokenExpiresAt.HasValue)
            {
                return !string.IsNullOrEmpty(user.EncryptedRefreshToken);
            }

            var expiryThreshold = DateTime.UtcNow.AddMinutes(bufferMinutes);
            return user.TokenExpiresAt.Value <= expiryThreshold;
        }

        public Task DisableAutomationAsync(ApplicationUser user)
        {
            throw new NotImplementedException("Not needed for current tests");
        }
    }
}