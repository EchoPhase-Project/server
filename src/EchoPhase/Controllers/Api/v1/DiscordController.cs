using EchoPhase.Clients.Discord;
using EchoPhase.Clients.Discord.Dto;
using EchoPhase.Identity;
using EchoPhase.Security.Antiforgery.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    namespace EchoPhase.Controllers
    {
        [ApiController]
        [Route("api/v1/discord/api")]
        public class DiscordController : ControllerBase
        {
            private readonly IDiscordClient _discord;
            private readonly IDiscordSecretVault _vault;
            private readonly IUserService _userService;

            public DiscordController(
                IDiscordClient discord,
                IDiscordSecretVault vault,
                IUserService userService)
            {
                _discord = discord;
                _vault = vault;
                _userService = userService;
            }

            private async Task<(string? token, IActionResult? error)> ResolveTokenAsync(string keyName)
            {
                var user = await _userService.GetAsync(User);
                if (user is null)
                    return (null, Unauthorized());

                var result = await _vault.GetAsync<string>(user.Id.ToString(), keyName);
                if (!result.Successful)
                    return (null, NotFound($"Token '{keyName}' not found."));

                return (result.Value, null);
            }

            [HttpGet("users/@me/guilds")]
            [ValidateAntiForgery]
            public async Task<IActionResult> GetGuildsAsync(
                [FromQuery] DiscordUserGuildsQueryDto query,
                [FromQuery] string keyName,
                CancellationToken ct = default)
            {
                var (token, error) = await ResolveTokenAsync(keyName);
                if (error is not null) return error;

                var result = await _discord.GetUserGuildsAsync(query, token!, ct);

                return result.Match(
                    guilds => Ok(guilds),
                    err    => StatusCode((int)err.Code, err.Message));
            }
        }
    }
}
