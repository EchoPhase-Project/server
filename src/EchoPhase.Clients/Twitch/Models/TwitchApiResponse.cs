// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Models;

namespace EchoPhase.Clients.Twitch.Models
{
    public class TwitchApiResponse<T> : ClientResponse<ITwitchApiResponseDto<T>, ITwitchApiError>, ITwitchApiResponse<T> where T : class
    {
        public TwitchApiResponse(IClientResponse<ITwitchApiResponseDto<T>, ITwitchApiError> inner)
        {
            this.Data = inner.Data;
            this.Error = inner.Error;
            this.StatusCode = inner.StatusCode;
        }
    }
}
