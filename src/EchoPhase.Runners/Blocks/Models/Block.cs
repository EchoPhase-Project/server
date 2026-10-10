// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace EchoPhase.Runners.Blocks.Models
{
    public class Block : IBlock
    {
        public int Id { get; set; }

        public BlockTypes Type { get; set; }

        public int Version { get; set; } = 1;

        public object Params { get; set; } = new();

        /// <summary>Label for people; expressions refer to blocks by id (blocks.b3), so renaming breaks nothing.</summary>
        public string? Name { get; set; }

        public string? Notes { get; set; }

        /// <summary>Skipped when run: its input passes through to its first output.</summary>
        public bool Disabled { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public BlockErrorPolicy OnError { get; set; } = BlockErrorPolicy.Stop;

        public BlockRetry? Retry { get; set; }

        /// <summary>Where the editor draws it; the runner ignores it.</summary>
        public BlockPosition? Position { get; set; }
    }

    public sealed record BlockPosition(double X, double Y);

    /// <summary>Tries after a failure (the first try included), and the wait between them.</summary>
    public sealed record BlockRetry(int MaxTries = 3, int DelayMs = 1000);

    public enum BlockErrorPolicy
    {
        /// <summary>The run stops and fails.</summary>
        Stop,

        /// <summary>The error becomes the block's output and the run goes on through its first output.</summary>
        Continue,

        /// <summary>The error becomes the block's output, sent through its "error" output.</summary>
        ErrorOutput
    }
}
