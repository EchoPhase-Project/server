// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Models;

namespace EchoPhase.Clients.Twitch.Models
{
    public interface ITwitchApiResponse<out T> : IClientResponse<ITwitchApiResponseDto<T>, ITwitchApiError>
    {
    }
}
