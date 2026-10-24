// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Runners.Blocks.Contexts
{
    public interface IBlockExecutionContext
    {
        /// <summary>The program being run.</summary>
        BlockGraph Graph { get; }

        BlockProgramSettings Settings { get; }

        /// <summary>Named values: Set writes them, expressions read them.</summary>
        IDictionary<string, object> Variables { get; }

        /// <summary>Each block's latest output by "b{id}"; expressions read it as blocks.b3.</summary>
        IDictionary<string, object?> BlockOutputs { get; }

        IList<string> Output { get; }

        /// <summary>Failures that stopped the run (or a branch); handled ones are block outputs instead.</summary>
        IList<BlockError> Errors { get; }

        /// <summary>Every block failure so far (after its last try), handled or not; expressions read it as errors.</summary>
        IList<BlockError> Failures { get; }

        IServiceProvider Services { get; }

        Guid UserId { get; }

        /// <summary>The stored run this executes, for observers; empty outside one.</summary>
        Guid RunId { get; }

        /// <summary>Aborts the run (e.g. the HTTP request was cancelled); checked between blocks.</summary>
        CancellationToken CancellationToken { get; }

        BlockLimits Limits { get; }

        /// <summary>Blocks executed so far in this run, loop iterations included.</summary>
        int Steps { get; set; }

        /// <summary>Nesting: 0 for the program, +1 inside each branch a block runs (loop bodies).</summary>
        int Depth { get; set; }

        /// <summary>0 for a run; +1 for each ExecuteWorkflow that led here.</summary>
        int CallDepth { get; }

        /// <summary>The output of the block that finished last: a run's (or sub-workflow's) result.</summary>
        object? LastOutput { get; set; }

        /// <summary>The waiting webhook caller, when the run answers one (RespondToWebhook).</summary>
        WebhookReply? Reply { get; }
    }
}
