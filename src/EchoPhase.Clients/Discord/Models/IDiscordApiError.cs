// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Clients.Discord.Models
{
    public interface IDiscordApiError
    {
        int Code
        {
            get; set;
        }
        string? Message
        {
            get; set;
        }
        Dictionary<string, string[]>? Errors
        {
            get; set;
        }
    }
}
