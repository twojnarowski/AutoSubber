using AutoSubber.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Polly;

namespace AutoSubber.Services
{
    /// <summary>
    /// Implementation of YouTube playlist management service
    /// </summary>
    public class YouTubePlaylistService : IYouTubePlaylistService
    {
        private readonly ILogger<YouTubePlaylistService> _logger;
        private readonly ITokenEncryptionService _tokenEncryption;

        public YouTubePlaylistService(
            ILogger<YouTubePlaylistService> logger,
            ITokenEncryptionService tokenEncryption)
        {
            _logger = logger;
            _tokenEncryption = tokenEncryption;
        }

        public async Task<string?> CreateAutoWatchLaterPlaylistAsync(ApplicationUser user)
        {
            try
            {
                // Validate user has required tokens
                if (string.IsNullOrEmpty(user.EncryptedAccessToken))
                {
                    _logger.LogWarning("User {UserId} does not have an access token", user.Id);
                    return null;
                }

                // Decrypt the access token
                var accessToken = _tokenEncryption.Decrypt(user.EncryptedAccessToken);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("Failed to decrypt access token for user {UserId}", user.Id);
                    return null;
                }

                // Create YouTube service with OAuth2 credentials
                var credential = GoogleCredential.FromAccessToken(accessToken);
                var youtubeService = new YouTubeService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "AutoSubber"
                });

                // Create the playlist name
                var playlistName = $"Auto Watch Later — {user.UserName}";

                // Create playlist object
                var newPlaylist = new Playlist()
                {
                    Snippet = new PlaylistSnippet()
                    {
                        Title = playlistName,
                        Description = "Automatically managed watch later playlist created by AutoSubber",
                        DefaultLanguage = "en"
                    },
                    Status = new PlaylistStatus()
                    {
                        PrivacyStatus = "private"
                    }
                };

                // Insert the playlist
                var insertRequest = youtubeService.Playlists.Insert(newPlaylist, "snippet,status");
                var insertResponse = await insertRequest.ExecuteAsync();

                if (insertResponse?.Id != null)
                {
                    _logger.LogInformation("Successfully created Auto Watch Later playlist {PlaylistId} for user {UserId}", 
                        insertResponse.Id, user.Id);
                    return insertResponse.Id;
                }
                else
                {
                    _logger.LogError("YouTube API returned null response when creating playlist for user {UserId}", user.Id);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Auto Watch Later playlist for user {UserId}", user.Id);
                return null;
            }
        }

        public async Task<bool> AddVideoToPlaylistAsync(ApplicationUser user, string videoId, string channelId, string? videoTitle = null)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(videoId) || string.IsNullOrEmpty(channelId))
                {
                    _logger.LogWarning("Invalid video or channel ID provided for user {UserId}", user.Id);
                    return false;
                }

                // Check if user has a playlist configured
                if (string.IsNullOrEmpty(user.AutoWatchLaterPlaylistId))
                {
                    _logger.LogWarning("User {UserId} does not have an Auto Watch Later playlist configured", user.Id);
                    return false;
                }

                // Validate user has required tokens
                if (string.IsNullOrEmpty(user.EncryptedAccessToken))
                {
                    _logger.LogWarning("User {UserId} does not have an access token for video {VideoId}", user.Id, videoId);
                    return false;
                }

                // Decrypt the access token
                var accessToken = _tokenEncryption.Decrypt(user.EncryptedAccessToken);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("Failed to decrypt access token for user {UserId} for video {VideoId}", user.Id, videoId);
                    return false;
                }

                // Create YouTube service with OAuth2 credentials
                var credential = GoogleCredential.FromAccessToken(accessToken);
                var youtubeService = new YouTubeService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "AutoSubber"
                });

                // Create playlist item to add video
                var playlistItem = new PlaylistItem()
                {
                    Snippet = new PlaylistItemSnippet()
                    {
                        PlaylistId = user.AutoWatchLaterPlaylistId,
                        ResourceId = new ResourceId()
                        {
                            Kind = "youtube#video",
                            VideoId = videoId
                        }
                    }
                };

                // Define retry policy for API calls
                var retryPolicy = Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(
                        retryCount: 3,
                        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                        onRetry: (outcome, timespan, retryCount, context) =>
                        {
                            _logger.LogWarning("Retry {RetryCount} for adding video {VideoId} to playlist for user {UserId} after {Delay}s. Exception: {Exception}",
                                retryCount, videoId, user.Id, timespan.TotalSeconds, outcome?.Message);
                        });

                // Execute with retry policy
                var result = await retryPolicy.ExecuteAsync(async () =>
                {
                    var insertRequest = youtubeService.PlaylistItems.Insert(playlistItem, "snippet");
                    return await insertRequest.ExecuteAsync();
                });

                if (result?.Id != null)
                {
                    _logger.LogInformation("Successfully added video {VideoId} (Title: {Title}) from channel {ChannelId} to playlist {PlaylistId} for user {UserId}",
                        videoId, videoTitle ?? "Unknown", channelId, user.AutoWatchLaterPlaylistId, user.Id);
                    return true;
                }
                else
                {
                    _logger.LogError("YouTube API returned null response when adding video {VideoId} to playlist for user {UserId}",
                        videoId, user.Id);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding video {VideoId} (Title: {Title}) from channel {ChannelId} to playlist for user {UserId}",
                    videoId, videoTitle ?? "Unknown", channelId, user.Id);
                return false;
            }
        }
    }
}