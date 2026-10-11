// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Server.Controllers.Api.v1.ApiClients
{
    /// <param name="Name">Display name, e.g. the bot or server it runs on.</param>
    /// <param name="Claims">Restrictions applied to every token the client obtains.</param>
    /// <param name="ExpiresAt">Optional; the client stops working afterwards. With Z or an offset.</param>
    public sealed record CreateApiClientRequest(
        [Required][MaxLength(64)] string Name,
        ApiClientClaims? Claims = null,
        DateTimeOffset? ExpiresAt = null
    );
}
