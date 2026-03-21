// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Models;
using EchoPhase.Clients.Twitch.Models;

namespace EchoPhase.Clients.Twitch.Extensions
{
    public static class TwitchClientExtensions
    {
        public static ITwitchApiResponse<T> ToTwitchApiResponse<T>(
            this IClientResponse<ITwitchApiResponseDto<T>, TwitchApiError> response
        ) where T : class => new TwitchApiResponse<T>(response);
    }
}
