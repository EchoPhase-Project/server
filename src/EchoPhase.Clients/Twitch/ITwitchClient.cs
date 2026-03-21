// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Twitch.Dto;

namespace EchoPhase.Clients.Twitch
{
    public interface ITwitchClient
    {
        Task<PagedResult<IEnumerable<TwitchVipResponseDto>>> GetVipsAsync(
            TwitchVipRequestQueryDto query,
            string bearerToken,
            CancellationToken ct = default);
    }
}
