// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net.WebSockets;

namespace EchoPhase.WebSockets.Constants
{
    /// <summary>
    /// Gateway close codes of our own, in the range RFC 6455 leaves to
    /// applications (4000-4999). The server also closes with standard codes:
    /// 1000 (client said bye), 1001 (shutting down), 1003 (not text), 1008
    /// (credentials expired or revoked, heartbeat timeout: refresh and
    /// reconnect), 1009 (message too big).
    /// </summary>
    public static class CloseCodes
    {
        /// <summary>
        /// The session the connection was opened with ended (signed out,
        /// revoked): the client is signed out and must not reconnect.
        /// </summary>
        public const WebSocketCloseStatus SessionEnded = (WebSocketCloseStatus)4000;

        public const string SessionEndedReason = "Session ended";
    }
}
