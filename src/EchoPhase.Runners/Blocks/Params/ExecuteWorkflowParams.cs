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
        /// <summary>One of the owner's workflows; it starts from its manual start (ManualTrigger or roots).</summary>
        [JsonPropertyName("workflowId")]
        public Guid WorkflowId { get; set; }

        /// <summary>Its input: a template ("@input.user") or a JSON value; absent: this block's input.</summary>
        [JsonPropertyName("input")]
        public JsonElement Input { get; set; }

        public IValidationResult Validate()
        {
            if (WorkflowId == Guid.Empty)
                return ValidationResult.Failure(error =>
                    error.Set(nameof(WorkflowId), "WorkflowId is required."));

            return ValidationResult.Success();
        }
    }
}
