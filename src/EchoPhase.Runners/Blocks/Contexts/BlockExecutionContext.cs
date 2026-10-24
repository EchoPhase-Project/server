// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Runners.Blocks.Contexts
{
    public class BlockExecutionContext : IBlockExecutionContext
    {
        public BlockExecutionContext(BlockProgram program, IServiceProvider services, Guid userId, CancellationToken ct = default)
        {
            Graph = new BlockGraph(program.Blocks, program.Connections);
            Settings = program.Settings;
            Services = services;
            UserId = userId;
            CancellationToken = ct;
        }

        public BlockGraph Graph { get; }
        public BlockProgramSettings Settings { get; }
        public IDictionary<string, object> Variables { get; init; } = new Dictionary<string, object>();
        public IDictionary<string, object?> BlockOutputs { get; } = new Dictionary<string, object?>();
        public IList<string> Output { get; } = [];
        public IList<BlockError> Errors { get; } = [];
        public IList<BlockError> Failures { get; } = [];
        public IServiceProvider Services { get; }
        public Guid UserId { get; }
        public Guid RunId { get; init; }
        public CancellationToken CancellationToken { get; }
        public BlockLimits Limits { get; init; } = BlockLimits.Unlimited;
        public int Steps { get; set; }
        public int Depth { get; set; }
        public int CallDepth { get; init; }
        public object? LastOutput { get; set; }
        public WebhookReply? Reply { get; init; }

        public static string OutputKey(int blockId) => $"b{blockId}";
    }
}
