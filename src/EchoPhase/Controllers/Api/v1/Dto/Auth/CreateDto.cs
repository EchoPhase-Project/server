// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Dto.Auth
{
    public record CreateDto(
        [Required][MinLength(12)] string DeviceId,
        ClaimsDto? Claims = null
    );
}
