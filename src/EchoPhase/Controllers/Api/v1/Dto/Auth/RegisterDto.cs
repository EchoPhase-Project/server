// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Dto.Auth
{
    public record RegisterDto(
        [Required][MinLength(2)] string Name,
        [Required][MinLength(3)] string Username,
        [Required][MinLength(6)] string Password
    );
}
