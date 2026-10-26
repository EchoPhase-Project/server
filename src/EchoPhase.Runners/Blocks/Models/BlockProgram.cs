// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>A program: blocks, the edges between them and how it runs.</summary>
    public class BlockProgram
    {
        public List<Block> Blocks { get; set; } = [];

        public List<BlockConnection> Connections { get; set; } = [];

        /// <summary>Where a manual run starts; empty: every block without inputs that is not an event trigger.</summary>
        public HashSet<int> StartIds { get; set; } = [];

        public BlockProgramSettings Settings { get; set; } = new();
    }

    public class BlockProgramSettings
    {
        /// <summary>Run time limit in seconds; null: the server's (never more).</summary>
        public int? TimeoutSeconds { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ExecutionDataPolicy SaveExecutionData { get; set; } = ExecutionDataPolicy.All;

        /// <summary>IANA zone for schedules (and the program's notion of "today"); null: the owner's.</summary>
        public string? TimeZone { get; set; }
    }

    /// <summary>Which runs keep per-block execution data (inputs, outputs, timings).</summary>
    public enum ExecutionDataPolicy
    {
        All,
        Errors,
        None
    }
}
