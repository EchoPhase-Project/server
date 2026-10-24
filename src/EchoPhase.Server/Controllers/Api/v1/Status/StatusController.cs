// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Redis.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Status
{
    /// <summary>
    /// Whether the service is open: anyone may ask, during maintenance too, so
    /// the webapp's maintenance screen knows when to let people back in.
    /// </summary>
    [Route("api/v1/status")]
    public class StatusController : ApiControllerBase
    {
        private readonly MaintenanceSwitch _maintenance;
        private readonly TimeProvider _time;

        public StatusController(MaintenanceSwitch maintenance, TimeProvider time)
        {
            _maintenance = maintenance;
            _time = time;
        }

        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(CancellationToken ct = default)
        {
            Response.Headers.CacheControl = "no-store";
            var state = await _maintenance.CurrentAsync(ct);
            return Ok(new
            {
                maintenance = state is not null,
                message = state?.Message,
                since = state?.Since,
                until = state?.Until,
                retryAfterSeconds = state is null ? (long?)null : (long)state.RetryAfter(_time.GetUtcNow()).TotalSeconds
            });
        }
    }
}
