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
        /// <summary>Intent names to receive, e.g. ["blocks", "connections"]; omitted: keep the current ones.</summary>
        public string[]? Intents
        {
            get; set;
        }

        public HandshakePayload()
        {
        }

        public IValidationResult Validate()
        {
            if (Intents is { } list && (list.Length > IntentLimits.MaxIntents || list.Any(n => n?.Length > IntentLimits.MaxNameLength)))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Intents), $"At most {IntentLimits.MaxIntents} intents of up to {IntentLimits.MaxNameLength} characters."));

            if (Intents is not null && Intents.Any(string.IsNullOrWhiteSpace))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Intents), "Intent names cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
