// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Server.Services.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace EchoPhase.Server.Controllers.Api.v1.Meta
{
    /// <param name="Version">This build: its image tag, e.g. "0.6.0-3-gabc1234".</param>
    /// <param name="Api">
    /// The contract series it serves, e.g. "0.6": a client built for 0.6.x works
    /// with it, one built for another series does not. Null for a local build.
    /// </param>
    public sealed record VersionResponse(string Version, string? Api);

    [Route("api/v1/version")]
    public class VersionController : ApiControllerBase
    {
        /// <summary>This server's version and contract series; anyone may ask.</summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
        public IActionResult Get()
        {
            // Changes only with a deploy: a minute in caches is plenty.
            Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromMinutes(1),
            };
            return Ok(new VersionResponse(ServerVersion.Current, ServerVersion.Api));
        }
    }
}
