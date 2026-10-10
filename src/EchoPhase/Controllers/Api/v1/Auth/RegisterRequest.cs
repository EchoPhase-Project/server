// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Auth
{
    public sealed record RegisterRequest(
        [Required][MinLength(2)] string Name,
        [Required][MinLength(3)] string Username,
        [Required][MinLength(6)] string Password,
        // IANA zone, e.g. from Intl.DateTimeFormat().resolvedOptions().timeZone; UTC when omitted.
        [MaxLength(64)] string? TimeZone = null
    );
}
