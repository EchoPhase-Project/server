// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Validation;
using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    [OpCodePayload(OpCodes.Handshake)]
    public class HandshakePayload : IPayload
    {
        public string? Intents
        {
            get; set;
        }

        public HandshakePayload()
        {
        }

        public IValidationResult Validate()
        {
            if (Intents != null && string.IsNullOrWhiteSpace(Intents))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Intents), "Intents cannot be empty if set."));

            return ValidationResult.Success();
        }
    }
}
