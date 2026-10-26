// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Runners.Blocks.Handlers;

namespace EchoPhase.Runners.Blocks.Models
{
    /// <summary>A program as a workflow stores it (its definition), in the binder's JSON options.</summary>
    public static class BlockProgramJson
    {
        public static BlockProgram Parse(string definition)
            => JsonSerializer.Deserialize<BlockProgram>(definition, BlockParamsBinder.Options) ?? new BlockProgram();
    }
}
