// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Clients.Twitch.Dto
{
    public class TwitchUserResponseDto
    {
        public List<TwitchUserDto> Data { get; set; } = new List<TwitchUserDto>();
    }
}
