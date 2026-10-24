// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Runners.Blocks.Contexts;
using EchoPhase.Runners.Blocks.Models;
using EchoPhase.Runners.Blocks.Observers;
using EchoPhase.Runners.Blocks.Params;
using EchoPhase.Runners.Blocks.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Runners.Blocks.Handlers
{
    /// <summary>A sub-workflow failed; retryable, unlike a StopAndError.</summary>
    public sealed class SubWorkflowFailedException(string message) : Exception(message);

    /// <summary>
    /// Runs another workflow inside this run: its own variables, but this
    /// run's steps, output, limits and cancellation, so a sub-workflow cannot
    /// buy a run more room. Nesting is bounded (a workflow may call itself).
    /// </summary>
    [BlockType(BlockTypes.ExecuteWorkflow,
        Description = "Runs one of the owner's workflows from its manual start and waits; output: its last block's output.")]
    public class ExecuteWorkflowHandler : BlockHandlerBase<ExecuteWorkflowParams>
    {
        public const int MaxCallDepth = 5;

        public override async Task<BlockResult> ExecuteAsync(BlockCall call, ExecuteWorkflowParams param)
        {
            var context = call.Context;
            if (context.CallDepth >= MaxCallDepth)
                throw new InvalidOperationException($"Workflows may call each other at most {MaxCallDepth} deep.");

            var source = context.Services.GetService<IWorkflowSource>()
                ?? throw new InvalidOperationException("Workflows cannot be called here.");
            var program = await source.FindAsync(context.UserId, param.WorkflowId, context.CancellationToken)
                ?? throw new InvalidOperationException($"Workflow {param.WorkflowId} not found.");

            // Saved programs were valid when saved; block types may have changed since.
            var validator = GetService<BlockProgramValidator>(context);
            if (validator.Validate(program, int.MaxValue) is [var issue, ..])
                throw new InvalidOperationException($"Workflow {param.WorkflowId} is invalid: {issue.Message}");
            var startIds = validator.ManualStartIds(program);
            if (startIds.Count == 0)
                throw new InvalidOperationException($"Workflow {param.WorkflowId} has nothing to start: add a ManualTrigger.");

            var input = param.Input.ValueKind == JsonValueKind.Undefined ? call.Input : BlockValues.Evaluate(param.Input, call.Scope);
            var sub = new BlockExecutionContext(program, context.Services, context.UserId, context.CancellationToken)
            {
                Limits = context.Limits,
                RunId = context.RunId,
                CallDepth = context.CallDepth + 1,
                Steps = context.Steps
            };

            // No observers: the sub-workflow's block ids would read as this program's.
            var runner = ActivatorUtilities.CreateInstance<BlocksRunner>(context.Services, Array.Empty<IBlockRunObserver>());
            await runner.RunAsync(sub, startIds, input);

            context.Steps = sub.Steps;
            foreach (var line in sub.Output)
                context.Output.Add(line);

            if (sub.Errors is [var error, ..])
            {
                if (error.Type == "LimitExceeded")
                    throw new BlockLimitExceededException(call.Block.Id, error.Message);
                throw new SubWorkflowFailedException(
                    $"Workflow {param.WorkflowId} failed at block {error.Id}: {error.Type}: {error.Message}");
            }

            if (context.Output.Count > context.Limits.MaxOutputEntries)
                throw new BlockLimitExceededException(call.Block.Id, $"Output exceeded {context.Limits.MaxOutputEntries} entries.");

            return BlockResult.Main(sub.LastOutput);
        }
    }
}
