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
        [ProducesResponseType<ServiceStatusResponse>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(CancellationToken ct = default)
        {
            Response.Headers.CacheControl = "no-store";
            var state = await _maintenance.CurrentAsync(ct);
            return Ok(ServiceStatusResponse.From(state, _time.GetUtcNow()));
        }
    }

    /// <summary>Whether the service is open, and if not, until when.</summary>
    /// <param name="Maintenance">True while the service is closed for maintenance.</param>
    /// <param name="Message">The maintenance message for users.</param>
    /// <param name="Since">When the maintenance started.</param>
    /// <param name="Until">When it is planned to end; none: not known.</param>
    /// <param name="RetryAfterSeconds">How long a client should wait before it asks again.</param>
    public sealed record ServiceStatusResponse(
        bool Maintenance, string? Message, DateTimeOffset? Since, DateTimeOffset? Until, long? RetryAfterSeconds)
    {
        public static ServiceStatusResponse From(MaintenanceState? state, DateTimeOffset now) => state is null
            ? new(false, null, null, null, null)
            : new(true, state.Message, state.Since, state.Until, (long)state.RetryAfter(now).TotalSeconds);
    }
}
