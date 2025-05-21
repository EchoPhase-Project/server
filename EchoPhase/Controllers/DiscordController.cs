using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

using EchoPhase.Clients;
using EchoPhase.Dtos;
using EchoPhase.Interfaces;

namespace EchoPhase.Controllers
{
    [ApiController]
    [Route("api/discord")]
    public class DiscordController : ControllerBase
    {
        private readonly DiscordClient _discordClient;

        public DiscordController(DiscordClient discordClient)
        {
            _discordClient = discordClient;
        }

        [HttpGet("guilds")]
        public async Task<IActionResult> GetUserGuilds([FromQuery] DiscordUserGuildsQueryDto query)
        {
            IDiscordApiResponse<List<DiscordGuildResponseDto>> response = await _discordClient.GetUserGuildsAsync(query);

            if (response.Error != null)
            {
                return StatusCode((int)response.StatusCode, response.Error);
            }

            return Ok(response.Data);
        }
    }
}

