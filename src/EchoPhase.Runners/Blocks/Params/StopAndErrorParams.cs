// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;
using EchoPhase.Configuration;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    public class StopAndErrorParams : IValidatable
    {
        public const string DefaultType = "StopAndError";

        /// <summary>A template: "Order @input.id has no items".</summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>The error's type, for telling failures apart (errors[0].type).</summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = DefaultType;

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Message))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Message), "Message cannot be empty."));

            if (string.IsNullOrWhiteSpace(Type) || Type.Length > 64)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Type), "Type must be 1-64 characters."));

            return ValidationResult.Success();
        }
    }
}
