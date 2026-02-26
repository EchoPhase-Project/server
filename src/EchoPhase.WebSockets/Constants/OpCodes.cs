using EchoPhase.WebSockets.Attributes;

namespace EchoPhase.WebSockets.Constants
{
    public enum OpCodes
    {
        Handshake = 0,

        [IgnoreOpCode]
        HandshakeAck = 1,

        Disconnect = 2,

        [IgnoreOpCode]
        DisconnectAck = 3,

        Ping = 4,

        [IgnoreOpCode]
        Pong = 5,

        Adjust = 6,

        [IgnoreOpCode]
        AdjustAck = 7,

        [IgnoreOpCode]
        Restricted = 8,

        [IgnoreOpCode]
        Error = 9,

        [IgnoreOpCode]
        Unknown = 0xffff,
    }
}
