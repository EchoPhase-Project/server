// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Discord;
using EchoPhase.Clients.Discord.Dto;
using EchoPhase.Connections;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
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
        [ProducesResponseType<DiscordUserGuildsResponseDto[]>(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType(typeof(ErrorBody))]
        public async Task<IActionResult> GetGuildsAsync(
            [FromQuery] DiscordUserGuildsQueryDto query,
            [FromQuery] string connection,
            CancellationToken ct = default)
        {
            var result = await _discord.GetUserGuildsAsync(
                query, new ConnectionRef(User.GetUserId(), connection), ct);

            return result.Match(
                guilds => Ok(guilds),
                // The upstream's status, with its message as an ErrorBody.
                err => Error(ConnectionErrors.Upstream, err.Message ?? "The provider refused the request.", (int)result.StatusCode));
        }
    }
}
