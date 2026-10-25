// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Server.Controllers.Api.v1.Maintenance
{
    /// <param name="Message">Shown to users; a default one when absent.</param>
    /// <param name="Minutes">It ends by itself after this long; absent: until turned off.</param>
    public sealed record EnableMaintenanceRequest(
        [StringLength(500)] string? Message,
        [Range(1, 7 * 24 * 60)] int? Minutes);
}
