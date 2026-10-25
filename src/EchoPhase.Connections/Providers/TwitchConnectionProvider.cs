// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using System.Net.Http.Headers;
using EchoPhase.Clients.Abstractions;
using EchoPhase.Clients.Twitch.Auth;
using EchoPhase.Clients.Twitch.Dto;
using EchoPhase.Configuration.Clients;
using EchoPhase.Connections.Models;
using EchoPhase.DAL.Postgres.Models;
using Microsoft.Extensions.Options;

namespace EchoPhase.Connections.Providers
{
    public class TwitchConnectionProvider : IOAuthConnectionProvider
    {
        private const string ClientIdHeader = "Client-Id";

        private readonly ITwitchAuthClient _auth;
        private readonly ClientsOptions _options;

        public TwitchConnectionProvider(ITwitchAuthClient auth, IOptions<ClientsOptions> options)
        {
            _auth = auth;
            _options = options.Value;
        }

        public string Name => ConnectionProviders.Twitch;

        public ConnectionAuthType ManualAuthType => ConnectionAuthType.OAuth2;

        public OAuthAppOptions PlatformApp => _options.Twitch;

        public void Apply(HttpRequestMessage request, ResolvedCredential credential)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);

            // Helix rejects requests whose Client-Id differs from the token's app.
            request.Headers.Remove(ClientIdHeader);
            if (credential.ClientId is not null)
                request.Headers.TryAddWithoutValidation(ClientIdHeader, credential.ClientId);
        }

        public async Task<TokenValidation> ValidateAsync(string accessToken, CancellationToken ct = default)
        {
            var result = await _auth.ValidateAsync(accessToken, ct);

            if (result.IsSuccess)
            {
                var v = result.Value;
                return TokenValidation.Valid(new TokenIdentity(
                    v.ClientId, v.UserId, v.Login, v.Scopes, v.ExpiresIn > 0 ? v.ExpiresIn : null));
            }

            return result.StatusCode == HttpStatusCode.Unauthorized
                ? TokenValidation.Invalid(result.Error?.Message)
                : TokenValidation.Unavailable(result.Error?.Message);
        }

        public string BuildAuthorizeUrl(OAuthClient client, string redirectUri, IEnumerable<string> scopes, string state)
            => _auth.BuildAuthorizeUrl(client.ClientId, redirectUri, scopes, state);

        public async Task<TokenGrant> ExchangeCodeAsync(
            OAuthClient client, string code, string redirectUri, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(client.ClientSecret))
                return TokenGrant.Rejected("The OAuth application has no client secret.");

            return ToGrant(await _auth.ExchangeCodeAsync(client.ClientId, client.ClientSecret, code, redirectUri, ct));
        }

        public async Task<TokenGrant> RefreshAsync(
            OAuthClient client, string refreshToken, CancellationToken ct = default)
            => ToGrant(await _auth.RefreshAsync(client.ClientId, client.ClientSecret, refreshToken, ct));

        // 400/401 from the token endpoint mean the code or refresh token (or the
        // app credentials) are not accepted; everything else may be transient.
        private static TokenGrant ToGrant(Result<TwitchTokenResponseDto> result)
        {
            if (result.IsSuccess)
            {
                var t = result.Value;
                return TokenGrant.Granted(new OAuthTokenSet(t.AccessToken, t.RefreshToken, t.Scopes, t.ExpiresIn));
            }

            return result.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                ? TokenGrant.Rejected(result.Error?.Message)
                : TokenGrant.Unavailable(result.Error?.Message);
        }
    }
}
