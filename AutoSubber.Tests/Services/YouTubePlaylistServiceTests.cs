using AutoSubber.Data;
using AutoSubber.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AutoSubber.Tests.Services
{
    public class YouTubePlaylistServiceTests
    {
        private readonly Mock<ILogger<YouTubePlaylistService>> _mockLogger;
        private readonly Mock<ITokenEncryptionService> _mockTokenEncryption;
        private readonly YouTubePlaylistService _playlistService;

        public YouTubePlaylistServiceTests()
        {
            _mockLogger = new Mock<ILogger<YouTubePlaylistService>>();
            _mockTokenEncryption = new Mock<ITokenEncryptionService>();
            _playlistService = new YouTubePlaylistService(_mockLogger.Object, _mockTokenEncryption.Object);
        }

        [Fact]
        public async Task CreateAutoWatchLaterPlaylistAsync_WithNullAccessToken_ReturnsNull()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedAccessToken = null
            };

            // Act
            var result = await _playlistService.CreateAutoWatchLaterPlaylistAsync(user);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateAutoWatchLaterPlaylistAsync_WithEmptyAccessToken_ReturnsNull()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedAccessToken = ""
            };

            // Act
            var result = await _playlistService.CreateAutoWatchLaterPlaylistAsync(user);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateAutoWatchLaterPlaylistAsync_WithFailedDecryption_ReturnsNull()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedAccessToken = "encrypted-token"
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-token"))
                              .Returns((string)null!);

            // Act
            var result = await _playlistService.CreateAutoWatchLaterPlaylistAsync(user);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateAutoWatchLaterPlaylistAsync_WithEmptyDecryptedToken_ReturnsNull()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedAccessToken = "encrypted-token"
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-token"))
                              .Returns(string.Empty);

            // Act
            var result = await _playlistService.CreateAutoWatchLaterPlaylistAsync(user);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task AddVideoToPlaylistAsync_WithNullAccessToken_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedAccessToken = null
            };

            // Act
            var result = await _playlistService.AddVideoToPlaylistAsync(user, "videoId", "channelId");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task AddVideoToPlaylistAsync_WithEmptyAccessToken_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedAccessToken = ""
            };

            // Act
            var result = await _playlistService.AddVideoToPlaylistAsync(user, "videoId", "channelId");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task AddVideoToPlaylistAsync_WithFailedDecryption_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedAccessToken = "encrypted-token"
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-token"))
                              .Returns((string)null!);

            // Act
            var result = await _playlistService.AddVideoToPlaylistAsync(user, "videoId", "channelId");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task AddVideoToPlaylistAsync_WithEmptyDecryptedToken_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedAccessToken = "encrypted-token"
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-token"))
                              .Returns(string.Empty);

            // Act
            var result = await _playlistService.AddVideoToPlaylistAsync(user, "videoId", "channelId");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task AddVideoToPlaylistAsync_WithNullPlaylistId_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedAccessToken = "encrypted-token",
                AutoWatchLaterPlaylistId = null
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-token"))
                              .Returns("valid-access-token");

            // Act
            var result = await _playlistService.AddVideoToPlaylistAsync(user, "videoId", "channelId");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task AddVideoToPlaylistAsync_WithEmptyPlaylistId_ReturnsFalse()
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedAccessToken = "encrypted-token",
                AutoWatchLaterPlaylistId = ""
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-token"))
                              .Returns("valid-access-token");

            // Act
            var result = await _playlistService.AddVideoToPlaylistAsync(user, "videoId", "channelId");

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AddVideoToPlaylistAsync_WithInvalidVideoId_ReturnsFalse(string? videoId)
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedAccessToken = "encrypted-token",
                AutoWatchLaterPlaylistId = "playlist123"
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-token"))
                              .Returns("valid-access-token");

            // Act
            var result = await _playlistService.AddVideoToPlaylistAsync(user, videoId!, "channelId");

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AddVideoToPlaylistAsync_WithInvalidChannelId_ReturnsFalse(string? channelId)
        {
            // Arrange
            var user = new ApplicationUser
            {
                Id = "user1",
                EncryptedAccessToken = "encrypted-token",
                AutoWatchLaterPlaylistId = "playlist123"
            };

            _mockTokenEncryption.Setup(x => x.Decrypt("encrypted-token"))
                              .Returns("valid-access-token");

            // Act
            var result = await _playlistService.AddVideoToPlaylistAsync(user, "videoId", channelId!);

            // Assert
            Assert.False(result);
        }
    }
}