using EchoPhase.Clients.Discord;
using EchoPhase.Clients.Discord.Dto;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/discord")]
    public class DiscordController : ControllerBase
    {
        private readonly DiscordClient _discordClient;
        private readonly ILogger<DiscordController> _logger;

        public DiscordController(DiscordClient discordClient, ILogger<DiscordController> logger)
        {
            _discordClient = discordClient;
            _logger = logger;
        }

        [HttpGet("@me/guilds")]
        public async Task<IActionResult> GetUserGuilds([FromQuery] DiscordUserGuildsQueryDto query)
        {
            var response = await _discordClient.GetUserGuildsAsync(query);

            if (response.Error is not null)
            {
                _logger.LogWarning("Discord API error {StatusCode}: {Error}", response.StatusCode, response.Error);
                return StatusCode((int)response.StatusCode, response.Error);
            }

            return Ok(response.Data);
        }
    }
}
