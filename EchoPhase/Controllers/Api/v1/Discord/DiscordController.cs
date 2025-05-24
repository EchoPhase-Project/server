using Microsoft.AspNetCore.Mvc;

using EchoPhase.Clients;
using EchoPhase.Dtos;

namespace EchoPhase.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class DiscordController : ControllerBase
    {
        private readonly DiscordClient _discordClient;

        public DiscordController(DiscordClient discordClient)
        {
            _discordClient = discordClient;
        }

        [HttpGet("@me/guilds")]
        public async Task<IActionResult> GetUserGuilds([FromQuery] DiscordUserGuildsQueryDto query)
        {
            var response = await _discordClient.GetUserGuildsAsync(query);

            if (response.Error != null)
            {
                return StatusCode((int)response.StatusCode, response.Error);
            }

            return Ok(response.Data);
        }
    }
}

