// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Runners.Blocks.Models
{
    public interface IBlock
    {
        int Id { get; }
        BlockTypes Type { get; }
        /// <summary>Behaviour version of the type; old programs keep theirs.</summary>
        int Version { get; }
        /// <summary>Type-specific settings, deserialized by the handler.</summary>
        object Params { get; }
        bool Disabled { get; }
        BlockErrorPolicy OnError { get; }
        BlockRetry? Retry { get; }
    }
}
