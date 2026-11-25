// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Nodes;
using EchoPhase.Runners.Blocks;
using EchoPhase.Runners.Blocks.Handlers;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Blocks
{
    /// <summary>The block types programs can use, for building an editor.</summary>
    [Route("api/v1/blocks/types")]
    public class BlockTypesController : ApiControllerBase
    {
        private readonly BlockTypeCatalog _catalog;

        public BlockTypesController(BlockTypeCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>
        /// Every type and version: its outputs (connections leave through
        /// these; any block with onError = ErrorOutput also has "error"),
        /// whether it starts runs, and its params as JSON Schema.
        /// </summary>
        [HttpGet]
        [ProducesResponseType<BlockTypeResponse[]>(StatusCodes.Status200OK)]
        public IActionResult List() => Ok(_catalog.Types.Select(BlockTypeResponse.From).ToList());
    }

    /// <summary>A block type as the editor sees it: what to draw and which form to show.</summary>
    /// <param name="Type">The type.</param>
    /// <param name="Version">Its version; a program names the version it was written for.</param>
    /// <param name="Description">What it does, for the block catalogue.</param>
    /// <param name="IsTrigger">Whether it starts runs (webhooks, schedules, events).</param>
    /// <param name="Outputs">The outputs connections leave through; with onError = ErrorOutput also "error".</param>
    /// <param name="ParamsSchema">Its params as JSON Schema, built from the params class the runner binds.</param>
    public sealed record BlockTypeResponse(
        BlockTypes Type, int Version, string Description, bool IsTrigger, IReadOnlyList<string> Outputs, JsonNode ParamsSchema)
    {
        public static BlockTypeResponse From(BlockTypeInfo info) => new(
            info.Type, info.Version, info.Description, info.IsTrigger, info.Outputs, info.ParamsSchema);
    }
}
