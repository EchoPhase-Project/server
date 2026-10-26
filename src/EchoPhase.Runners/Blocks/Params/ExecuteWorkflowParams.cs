// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using System.Text.Json.Serialization;
using EchoPhase.Configuration;
using EchoPhase.Types.Validation;

namespace EchoPhase.Runners.Blocks.Params
{
    public class ExecuteWorkflowParams : IValidatable
    {
        /// <summary>
        /// The key of a workflow in the same package (not its id: an installed
        /// copy keeps the keys); it starts from its manual start (ManualTrigger or roots).
        /// </summary>
        [JsonPropertyName("workflow")]
        public Guid Workflow { get; set; }

        /// <summary>Its input: a template ("@input.user") or a JSON value; absent: this block's input.</summary>
        [JsonPropertyName("input")]
        public JsonElement Input { get; set; }

        public IValidationResult Validate()
        {
            if (Workflow == Guid.Empty)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(Workflow), "Workflow (the key of a workflow in the package) is required."));

            return ValidationResult.Success();
        }
    }
}
