// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Types.Validation;
using EchoPhase.WebSockets.Attributes;
using EchoPhase.WebSockets.Constants;

namespace EchoPhase.WebSockets.Processors.Payloads
{
    [OpCodePayload(OpCodes.Error)]
    public class ErrorPayload : IPayload
    {
        public ErrorCodes Code { get; set; } = ErrorCodes.Undefined;
        public string Message { get; set; } = string.Empty;

        public ErrorPayload()
        {
        }

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Message))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Message), "Message cannot be null or empty."));

            if (!Enum.IsDefined(typeof(ErrorCodes), Code))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Code), "Invalid error code."));

            return ValidationResult.Success();
        }
    }
}
