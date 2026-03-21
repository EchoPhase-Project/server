// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using EchoPhase.Clients;
using EchoPhase.Clients.Discord;
using EchoPhase.Clients.Discord.Dto;
using EchoPhase.Clients.Providers;
using EchoPhase.Identity;
using EchoPhase.Security.Antiforgery.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Controllers.Api.v1
{
    [ApiController]
    [Route("api/v1/discord/api")]
    public class DiscordController : ControllerBase
    {
        private readonly IDiscordClient _discord;
        private readonly IExternalTokenService _tokenService;
        private readonly IUserService _userService;

        public DiscordController(
            IDiscordClient discord,
            IExternalTokenService tokenService,
            IUserService userService)
        {
            _discord = discord;
            _tokenService = tokenService;
            _userService = userService;
        }

        private async Task<(string? token, IActionResult? error)> ResolveTokenAsync(
            Guid userId,
            string providerName,
            string tokenName)
        {
            var result = await _tokenService.GetAsync(userId, providerName, tokenName);
            if (!result.Successful)
                return (null, NotFound($"Token '{providerName}:{tokenName}' not found."));

            return (Encoding.UTF8.GetString(result.Value!), null);
        }

        [HttpGet("users/@me/guilds")]
        [ValidateAntiForgery]
        public async Task<IActionResult> GetGuildsAsync(
            [FromQuery] DiscordUserGuildsQueryDto query,
            [FromQuery] string tokenName,
            CancellationToken ct = default)
        {
            var user = await _userService.GetAsync(User);
            if (user is null)
                return Unauthorized();

            var (token, error) = await ResolveTokenAsync(user.Id, DiscordTokenProvider.ProviderName, tokenName);
            if (error is not null) return error;

            if (token is not null)
            {
                var result = await _discord.GetUserGuildsAsync(query, token, ct);
                return result.Match(
                    guilds => Ok(guilds),
                    err => StatusCode(err.Code, err.Message));
            }

            return BadRequest();
        }
    }
}
