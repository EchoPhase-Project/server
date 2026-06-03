// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Serialization;
using EchoPhase.Configuration;
using EchoPhase.Types.Validation;
using EchoPhase.Clients.Discord.Dto;

namespace EchoPhase.Runners.Blocks.Params
{
    public class DiscordGetUserGuildsParams : IValidatable
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("token_name")]
        public string TokenName { get; set; } = string.Empty;

        [JsonPropertyName("query")]
        public DiscordUserGuildsQueryDto Query { get; set; } = new();

        [JsonPropertyName("next")]
        public IEnumerable<int> Next { get; set; } = new HashSet<int>();

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Name), "Name cannot be empty."));

            if (string.IsNullOrWhiteSpace(TokenName))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(TokenName), "TokenName cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
