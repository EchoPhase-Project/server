// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Security.Authentication.ApiClients
{
    /// <summary>
    /// Restrictions baked into every token the client obtains.
    /// </summary>
    public sealed record ApiClientGrant(
        IReadOnlyDictionary<string, string[]> Permissions);

    /// <summary>
    /// The secret is returned only here, once; it is never stored in plain text.
    /// </summary>
    public sealed record ApiClientCreated(ApiClient Client, string ClientSecret);

    public sealed record ClientAccessToken(string AccessToken, int ExpiresIn);
}
