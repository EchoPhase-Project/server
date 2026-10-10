// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.WebSockets.Processors.Payloads
{
    /// <summary>Bounds on the intent names a client sends in Handshake and Adjust.</summary>
    public static class IntentLimits
    {
        public const int MaxIntents = 64;
        public const int MaxNameLength = 64;
    }
}
