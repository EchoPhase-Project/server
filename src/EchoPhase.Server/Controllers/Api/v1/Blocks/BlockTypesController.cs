// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

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
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<IReadOnlyList<BlockTypeInfo>> List() => Ok(_catalog.Types);
    }
}
