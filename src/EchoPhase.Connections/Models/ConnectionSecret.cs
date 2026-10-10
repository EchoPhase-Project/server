// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text.Json.Serialization;

namespace EchoPhase.Connections.Models
{
    /// <summary>
    /// Plain-text token payload; only ever held in memory. A bot token is
    /// stored as <see cref="AccessToken"/> without a refresh token.
    /// </summary>
    public sealed record ConnectionSecret(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);
}
