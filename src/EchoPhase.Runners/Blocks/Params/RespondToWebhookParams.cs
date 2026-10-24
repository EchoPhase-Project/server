// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Serialization;
using EchoPhase.Configuration;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    public class RespondToWebhookParams : IValidatable
    {
        [JsonPropertyName("status")]
        public int Status { get; set; } = 200;

        /// <summary>A template ("@input", "Done: @(len(items))") or a JSON value; absent: this block's input. Text is sent as text, anything else as JSON.</summary>
        [JsonPropertyName("body")]
        public JsonElement Body { get; set; }

        /// <summary>Templates; only Content-Type, Cache-Control and X-* headers.</summary>
        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = [];

        public IValidationResult Validate()
        {
            if (Status is < 200 or > 599)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Status), "Status must be 200-599."));

            if (Headers.Keys.FirstOrDefault(h => !WebhookResponse.IsAllowedHeader(h)) is { } header)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Headers), $"Header '{header}' may not be set; use Content-Type, Cache-Control or X-* headers."));

            return ValidationResult.Success();
        }
    }
}
