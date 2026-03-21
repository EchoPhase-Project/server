// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Controllers.Api.v1.Dto.Discord
{
    public record DeleteVaultDto(
       [Required][MinLength(6)] string Name
    );
}
