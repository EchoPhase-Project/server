// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;
using EchoPhase.Clients.Discord.Dto;
using EchoPhase.Configuration;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    public class DiscordGetUserGuildsParams : IValidatable
    {
        /// <summary>
        /// Optional variable to also store the guilds in; they are always the
        /// block's output (blocks.b&lt;id&gt; or the next block's input).
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Name of the user's stored connection to call the API with.
        /// </summary>
        [JsonPropertyName("connection")]
        public string Connection { get; set; } = string.Empty;

        [JsonPropertyName("query")]
        public DiscordUserGuildsQueryDto Query { get; set; } = new();

        public IValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Connection))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Connection), "Connection cannot be empty."));

            return ValidationResult.Success();
        }
    }
}
