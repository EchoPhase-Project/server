// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

namespace EchoPhase.Connections.Models
{
    /// <summary>
    /// Credentials of the OAuth application a connection belongs to.
    /// <see cref="AppId"/> is <c>null</c> for the platform application.
    /// </summary>
    public sealed record OAuthClient(Guid? AppId, string ClientId, string? ClientSecret);
}
