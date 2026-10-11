// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Server.Controllers.Api.v1.OAuthApps
{
    public sealed record SaveOAuthAppRequest(
        [Required] string Provider,
        [Required][MaxLength(64)] string Name,
        [Required][MaxLength(128)] string ClientId,
        string? ClientSecret = null
    );
}
