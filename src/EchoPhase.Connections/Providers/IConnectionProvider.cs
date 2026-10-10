// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Connections.Models;
using EchoPhase.DAL.Postgres.Models;

namespace EchoPhase.Connections.Providers
{
    /// <summary>
    /// Provider-specific knowledge needed to authenticate requests with a
    /// stored connection.
    /// </summary>
    public interface IConnectionProvider
    {
        string Name { get; }

        /// <summary>
        /// The kind of connection created when the user pastes a token by hand.
        /// </summary>
        ConnectionAuthType ManualAuthType { get; }

        /// <summary>
        /// Sets the authentication headers on an outgoing request, replacing any
        /// set by a previous attempt.
        /// </summary>
        void Apply(HttpRequestMessage request, ResolvedCredential credential);

        Task<TokenValidation> ValidateAsync(string accessToken, CancellationToken ct = default);
    }

    /// <summary>
    /// A provider that supports the OAuth 2.0 authorisation-code and refresh grants.
    /// </summary>
    public interface IOAuthConnectionProvider : IConnectionProvider
    {
        /// <summary>
        /// The platform application from configuration, used when a connection
        /// does not reference a user-owned app.
        /// </summary>
        Configuration.Clients.OAuthAppOptions PlatformApp { get; }

        string BuildAuthorizeUrl(OAuthClient client, string redirectUri, IEnumerable<string> scopes, string state);

        Task<TokenGrant> ExchangeCodeAsync(OAuthClient client, string code, string redirectUri, CancellationToken ct = default);

        Task<TokenGrant> RefreshAsync(OAuthClient client, string refreshToken, CancellationToken ct = default);
    }
}
