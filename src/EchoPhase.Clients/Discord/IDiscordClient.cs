// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Discord.Dto;

namespace EchoPhase.Clients.Discord
{
    public interface IDiscordClient
    {
        Task<Result<IEnumerable<DiscordUserGuildsResponseDto>>> GetUserGuildsAsync(
            DiscordUserGuildsQueryDto query,
            string botToken,
            CancellationToken ct = default);
    }
}
