// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json;
using EchoPhase.Runners.Blocks.Models;

namespace EchoPhase.Server.Controllers.Api.v1.Blocks
{
    /// <summary>
    /// A program to run once, not saved: blocks, connections, settings, and
    /// optionally where to start and the input those first blocks get.
    /// </summary>
    public sealed class RunBlocksRequest : BlockProgram
    {
        /// <summary>The first blocks' input (a ManualTrigger's output).</summary>
        public JsonElement? Input { get; set; }
    }
}
