// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Clients.Twitch.Models
{
    public class TwitchApiError : ITwitchApiError
    {
        public string Error { set; get; } = string.Empty;
        public int Status
        {
            set; get;
        }
        public string Message { set; get; } = string.Empty;
    }
}
