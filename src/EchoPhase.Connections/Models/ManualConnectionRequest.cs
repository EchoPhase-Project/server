// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Connections.Models
{
    /// <summary>
    /// Tokens pasted by the user instead of going through the OAuth flow.
    /// For bot-token providers only <see cref="AccessToken"/> is used.
    /// </summary>
    public sealed record ManualConnectionRequest(
        string Name,
        string AccessToken,
        string? RefreshToken = null,
        string? ClientSecret = null);
}
