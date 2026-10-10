// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.WebSockets.Constants
{
    public enum ErrorCodes
    {
        Undefined,
        InvalidMessage,
        DeserializationError,
        TypeError,
        Unsupported,
        NotFound,
        InvalidPayload,

        /// <summary>The token's permissions do not allow subscribing to some intents.</summary>
        DisallowedIntents
    }
}
