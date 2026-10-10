// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Validation;
using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    [OpCodePayload(OpCodes.Adjust)]
    public class AdjustPayload : IPayload
    {
        /// <summary>The intent names to receive from now on; empty: none.</summary>
        public string[] Intents { get; set; } = [];

        public AdjustPayload()
        {
        }

        public IValidationResult Validate()
        {
            if (Intents is { } list && (list.Length > IntentLimits.MaxIntents || list.Any(n => n?.Length > IntentLimits.MaxNameLength)))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Intents), $"At most {IntentLimits.MaxIntents} intents of up to {IntentLimits.MaxNameLength} characters."));

            if (Intents is null || Intents.Any(string.IsNullOrWhiteSpace))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Intents), "Intents must be a list of names."));

            return ValidationResult.Success();
        }
    }
}
