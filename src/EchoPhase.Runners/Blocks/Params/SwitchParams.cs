// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;
using EchoPhase.Configuration;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    /// <summary>Params whose values add outputs to the block (beyond its type's own).</summary>
    public interface IHasOutputs
    {
        IReadOnlyList<string> ExtraOutputs { get; }
    }

    public class SwitchRule
    {
        /// <summary>An expression; the first rule that is true wins.</summary>
        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        /// <summary>The output it sends to; default: its position ("0", "1", ...).</summary>
        [JsonPropertyName("output")]
        public string? Output { get; set; }
    }

    public class SwitchParams : IValidatable, IHasOutputs
    {
        public const int MaxRules = 32;

        [JsonPropertyName("rules")]
        public List<SwitchRule> Rules { get; set; } = [];

        [JsonIgnore]
        public IReadOnlyList<string> ExtraOutputs
            => Rules.Select((rule, i) => string.IsNullOrWhiteSpace(rule.Output) ? i.ToString() : rule.Output.Trim()).ToList();

        public IValidationResult Validate()
        {
            if (Rules.Count is 0 or > MaxRules)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Rules), $"A switch needs 1-{MaxRules} rules."));

            if (Rules.Any(r => string.IsNullOrWhiteSpace(r.Condition)))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Rules), "Every rule needs a condition."));

            var outputs = ExtraOutputs;
            if (outputs.Distinct(StringComparer.Ordinal).Count() != outputs.Count)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Rules), "Rule outputs must differ."));

            if (outputs.Any(o => o is BlockOutputs.Fallback or BlockOutputs.Error || o.Length > 64))
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Rules), $"Rule outputs must be at most 64 characters and not \"{BlockOutputs.Fallback}\" or \"{BlockOutputs.Error}\"."));

            return ValidationResult.Success();
        }
    }
}
