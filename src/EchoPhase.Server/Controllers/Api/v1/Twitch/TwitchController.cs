// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Twitch;
using EchoPhase.Clients.Twitch.Dto;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Twitch
{
    [Route("api/v1/twitch/api")]
    public class TwitchController : ApiControllerBase
    {
        private readonly ITwitchClient _twitch;

        public TwitchController(ITwitchClient twitch)
        {
            _twitch = twitch;
        }

        [HttpGet("channels/vips")]
        [RequirePermission(Resources.Twitch, Permissions.Read)]
        [ProducesResponseType<TwitchVipsResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetVipsAsync(
            [FromQuery] TwitchVipRequestQueryDto query,
            [FromQuery] string connection,
            CancellationToken ct = default)
        {
            var result = await _twitch.GetVipsAsync(
                query, new ConnectionRef(User.GetUserId(), connection), ct);

            return result.Match(
                (vips, page) => Ok(new TwitchVipsResponse(vips.ToList(), new TwitchPaginationResponse(page?.Cursor))),
                err => StatusCode((int)result.StatusCode, err.Message));
        }
    }

    /// <summary>A page of a channel's VIPs, as Twitch returns it.</summary>
    /// <param name="Data">The VIPs on this page.</param>
    /// <param name="Pagination">Where the next page starts.</param>
    public sealed record TwitchVipsResponse(IReadOnlyList<TwitchVipResponseDto> Data, TwitchPaginationResponse Pagination);

    /// <summary>Twitch's cursor for the next page.</summary>
    /// <param name="Cursor">The cursor; none: this is the last page.</param>
    public sealed record TwitchPaginationResponse(string? Cursor);
}
