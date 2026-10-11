// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Discord;
using EchoPhase.Clients.Discord.Dto;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Discord
{
    [Route("api/v1/discord/api")]
    public class DiscordController : ApiControllerBase
    {
        private readonly IDiscordClient _discord;

        public DiscordController(IDiscordClient discord)
        {
            _discord = discord;
        }

        [HttpGet("users/@me/guilds")]
        [RequirePermission(Resources.Discord, Permissions.Read)]
        public async Task<IActionResult> GetGuildsAsync(
            [FromQuery] DiscordUserGuildsQueryDto query,
            [FromQuery] string connection,
            CancellationToken ct = default)
        {
            var result = await _discord.GetUserGuildsAsync(
                query, new ConnectionRef(User.GetUserId(), connection), ct);

            return result.Match(
                guilds => Ok(guilds),
                err => StatusCode((int)result.StatusCode, err.Message));
        }
    }
}
