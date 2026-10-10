// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;
using EchoPhase.Configuration;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    public enum MergeMode
    {
        /// <summary>One list: lists are concatenated, other values appended, in arrival order.</summary>
        Append,

        /// <summary>One object: each input under its source, { "b2": ..., "b5": ... }.</summary>
        BySource
    }

    public class MergeParams : IValidatable
    {
        [JsonPropertyName("mode")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public MergeMode Mode { get; set; } = MergeMode.Append;

        public IValidationResult Validate() => ValidationResult.Success();
    }
}
