// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Clients.Models;

namespace EchoPhase.Clients.Discord.Models
{
    public class DiscordApiResponse<T> : ClientResponse<T, IDiscordApiError>, IDiscordApiResponse<T> where T : class
    {
        public DiscordApiResponse(IClientResponse<T, IDiscordApiError> inner)
        {
            this.Data = inner.Data;
            this.Error = inner.Error;
            this.StatusCode = inner.StatusCode;
        }
    }
}
