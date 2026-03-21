// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.WebSockets.Constants
{
    public enum OpCodes
    {
        Handshake = 0,
        HandshakeAck = 1,
        Disconnect = 2,
        DisconnectAck = 3,
        Ping = 4,
        Pong = 5,
        Adjust = 6,
        AdjustAck = 7,
        Restricted = 8,
        Error = 9,
        Unknown = 0xffff,
    }
}
