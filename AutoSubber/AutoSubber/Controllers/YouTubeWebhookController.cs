using AutoSubber.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace AutoSubber.Controllers
{
    /// <summary>
    /// Controller for handling YouTube PubSubHubbub webhooks
    /// </summary>
    [ApiController]
    [Route("api/youtube")]
    public class YouTubeWebhookController : ControllerBase
    {
        private readonly IYouTubeWebhookService _webhookService;
        private readonly ILogger<YouTubeWebhookController> _logger;

        public YouTubeWebhookController(IYouTubeWebhookService webhookService, ILogger<YouTubeWebhookController> logger)
        {
            _webhookService = webhookService;
            _logger = logger;
        }

        /// <summary>
        /// Handles hub.challenge verification for PubSubHubbub subscription
        /// </summary>
        /// <param name="hub_mode">The hub mode (subscribe/unsubscribe)</param>
        /// <param name="hub_topic">The topic being subscribed to</param>
        /// <param name="hub_challenge">The challenge string to echo back</param>
        /// <param name="hub_lease_seconds">The lease duration</param>
        /// <returns>The challenge string for verification</returns>
        [HttpGet("webhook")]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string? hub_mode,
            [FromQuery(Name = "hub.topic")] string? hub_topic,
            [FromQuery(Name = "hub.challenge")] string? hub_challenge,
            [FromQuery(Name = "hub.lease_seconds")] string? hub_lease_seconds)
        {
            _logger.LogInformation("Received webhook verification request: mode={Mode}, topic={Topic}, challenge={Challenge}, lease={Lease}",
                hub_mode, hub_topic, hub_challenge, hub_lease_seconds);

            // Validate that this is a proper verification request
            if (string.IsNullOrEmpty(hub_mode) || string.IsNullOrEmpty(hub_challenge))
            {
                _logger.LogWarning("Invalid verification request - missing mode or challenge");
                return BadRequest("Invalid verification request");
            }

            // For YouTube PubSubHubbub, we should verify the topic is a YouTube channel feed
            if (!string.IsNullOrEmpty(hub_topic) && !hub_topic.Contains("youtube.com"))
            {
                _logger.LogWarning("Invalid topic for YouTube webhook: {Topic}", hub_topic);
                return BadRequest("Invalid topic");
            }

            // Echo back the challenge to verify the webhook endpoint
            _logger.LogInformation("Webhook verification successful, returning challenge");
            return Content(hub_challenge, "text/plain");
        }

        /// <summary>
        /// Handles incoming YouTube webhook notifications
        /// </summary>
        /// <returns>HTTP 200 if processed successfully</returns>
        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveWebhook()
        {
            try
            {
                _logger.LogInformation("Received YouTube webhook notification");

                // Read the XML payload from the request body
                using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                var xmlPayload = await reader.ReadToEndAsync();

                if (string.IsNullOrEmpty(xmlPayload))
                {
                    _logger.LogWarning("Received empty webhook payload");
                    return BadRequest("Empty payload");
                }

                _logger.LogDebug("Webhook payload: {Payload}", xmlPayload);

                // Process the webhook through the service
                var success = await _webhookService.ProcessWebhookAsync(xmlPayload);

                if (success)
                {
                    _logger.LogInformation("Successfully processed YouTube webhook notification");
                    return Ok();
                }
                else
                {
                    _logger.LogWarning("Failed to process YouTube webhook notification");
                    return StatusCode(500, "Failed to process webhook");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling YouTube webhook notification");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}