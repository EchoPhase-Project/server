// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Server.Controllers.Api.v1.Connections
{
    /// <param name="Name">Connection name; an existing name reconnects that connection.</param>
    /// <param name="AppId">A user-owned OAuth app; omit to use the platform app.</param>
    public sealed record AuthorizeConnectionRequest(
        [Required][RegularExpression(ConnectionNames.Pattern, ErrorMessage = ConnectionNames.Message)] string Name,
        Guid? AppId = null
    );
}
