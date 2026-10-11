// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel.DataAnnotations;

namespace EchoPhase.Server.Controllers.Api.v1.Connections
{
    /// <param name="Name">Connection name, unique per provider; reusing it replaces the connection.</param>
    /// <param name="AccessToken">Bot token for Discord; OAuth access token for Twitch.</param>
    /// <param name="RefreshToken">OAuth only; enables automatic refresh.</param>
    /// <param name="ClientSecret">OAuth only; secret of the app that issued the token.
    /// Omit for public clients or tokens issued to the platform app.</param>
    public sealed record SaveManualConnectionRequest(
        [Required][RegularExpression(ConnectionNames.Pattern, ErrorMessage = ConnectionNames.Message)] string Name,
        [Required] string AccessToken,
        string? RefreshToken = null,
        string? ClientSecret = null
    );
}
