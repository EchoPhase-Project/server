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
        public string Intents { get; set; } = string.Empty;

        public AdjustPayload()
        {
        }

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Intents))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Intents), "Intents cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
