// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Redis.Maintenance;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using Microsoft.AspNetCore.Mvc;

namespace EchoPhase.Server.Controllers.Api.v1.Maintenance
{
    /// <summary>
    /// The maintenance switch, for admins (maintenance:update): on at once for
    /// every instance, no deploy. The CLI does the same: <c>maintenance on</c>.
    /// </summary>
    [Route("api/v1/maintenance")]
    [RequirePermission(Resources.Maintenance, Permissions.Update)]
    public class MaintenanceController : ApiControllerBase
    {
        private readonly MaintenanceSwitch _maintenance;

        public MaintenanceController(MaintenanceSwitch maintenance)
        {
            _maintenance = maintenance;
        }

        /// <summary>Turns maintenance on (again: replaces message and end).</summary>
        [HttpPut]
        [ValidateAntiForgery]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Enable([FromBody] EnableMaintenanceRequest dto, CancellationToken ct = default)
        {
            var duration = dto.Minutes is { } minutes ? TimeSpan.FromMinutes(minutes) : (TimeSpan?)null;
            var state = await _maintenance.EnableAsync(dto.Message, duration, ct);
            return Ok(new { maintenance = true, message = state.Message, since = state.Since, until = state.Until });
        }

        [HttpDelete]
        [ValidateAntiForgery]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Disable(CancellationToken ct = default)
        {
            await _maintenance.DisableAsync(ct);
            return NoContent();
        }
    }
}
