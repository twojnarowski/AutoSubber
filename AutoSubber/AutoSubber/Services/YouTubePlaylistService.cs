using AutoSubber.Data;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

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
    }
}