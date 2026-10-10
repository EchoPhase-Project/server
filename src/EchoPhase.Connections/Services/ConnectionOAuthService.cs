// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EchoPhase.Connections.Models;
using EchoPhase.Connections.Providers;
using EchoPhase.Types.Result;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace EchoPhase.Connections.Services
{
    public class ConnectionOAuthService : IConnectionOAuthService
    {
        private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

        private readonly IConnectionProviderRegistry _providers;
        private readonly IOAuthAppService _apps;
        private readonly IConnectionService _connections;
        private readonly ITimeLimitedDataProtector _protector;

        public ConnectionOAuthService(
            IConnectionProviderRegistry providers,
            IOAuthAppService apps,
            IConnectionService connections,
            IDataProtectionProvider dataProtection)
        {
            _providers = providers;
            _apps = apps;
            _connections = connections;
            _protector = dataProtection
                .CreateProtector("EchoPhase.Connections.OAuthState")
                .ToTimeLimitedDataProtector();
        }

        private sealed record State(Guid UserId, string Provider, string Name, Guid? AppId, string Nonce);

        public async Task<IServiceResult<OAuthAuthorization>> BeginAsync(
            Guid userId, string provider, string name, Guid? appId, CancellationToken ct = default)
        {
            if (_providers.Find(provider) is not IOAuthConnectionProvider oauth)
                return Failure<OAuthAuthorization>(ConnectionErrors.UnknownProvider,
                    $"Provider '{provider}' does not support OAuth.");

            if (string.IsNullOrWhiteSpace(oauth.PlatformApp.RedirectUri))
                return Failure<OAuthAuthorization>(ConnectionErrors.NotConfigured,
                    $"No redirect URI is configured for {oauth.Name}.");

            if (appId is { } id && !await _apps.IsOwnedAsync(userId, oauth.Name, id, ct))
                return Failure<OAuthAuthorization>(ConnectionErrors.NotFound, "OAuth app not found.");

            var client = await _apps.ResolveClientAsync(oauth.Name, appId, ct);
            if (!client.Successful)
                return ServiceResult<OAuthAuthorization>.Failure(client.Error);

            if (string.IsNullOrEmpty(client.Value!.ClientSecret))
                return Failure<OAuthAuthorization>(ConnectionErrors.Invalid,
                    "The OAuth app needs a client secret to complete the authorisation-code flow.");

            var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var state = _protector.Protect(
                JsonSerializer.Serialize(new State(userId, oauth.Name, name, appId, nonce)),
                StateLifetime);

            var url = oauth.BuildAuthorizeUrl(client.Value, oauth.PlatformApp.RedirectUri, oauth.PlatformApp.Scopes, state);
            return ServiceResult<OAuthAuthorization>.Success(new OAuthAuthorization(url, nonce));
        }

        public async Task<IServiceResult<ConnectionSummary>> CompleteAsync(
            string provider, string code, string state, string? nonce, CancellationToken ct = default)
        {
            State? payload;
            try
            {
                payload = JsonSerializer.Deserialize<State>(_protector.Unprotect(state));
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException)
            {
                return Failure<ConnectionSummary>(ConnectionErrors.Invalid, "The authorisation request is invalid or has expired.");
            }

            // The nonce cookie ties the callback to the browser that started the
            // flow, so a victim cannot be tricked into linking their account to
            // someone else's EchoPhase user.
            if (payload is null
                || nonce is null
                || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(payload.Nonce), Encoding.UTF8.GetBytes(nonce))
                || !string.Equals(payload.Provider, provider, StringComparison.OrdinalIgnoreCase))
                return Failure<ConnectionSummary>(ConnectionErrors.Invalid, "The authorisation request does not match this browser.");

            if (_providers.Find(payload.Provider) is not IOAuthConnectionProvider oauth)
                return Failure<ConnectionSummary>(ConnectionErrors.UnknownProvider, $"Unknown provider '{payload.Provider}'.");

            var client = await _apps.ResolveClientAsync(oauth.Name, payload.AppId, ct);
            if (!client.Successful)
                return ServiceResult<ConnectionSummary>.Failure(client.Error);

            var grant = await oauth.ExchangeCodeAsync(client.Value!, code, oauth.PlatformApp.RedirectUri!, ct);
            if (grant.Status != TokenGrantStatus.Granted)
                return Failure<ConnectionSummary>(
                    grant.Status == TokenGrantStatus.Rejected ? ConnectionErrors.Invalid : ConnectionErrors.Upstream,
                    $"Could not obtain tokens: {grant.Error}");

            // Fetch the account identity; a failure here is not fatal.
            var validation = await oauth.ValidateAsync(grant.Tokens!.AccessToken, ct);
            var identity = validation.Status == TokenValidationStatus.Valid ? validation.Identity : null;

            return await _connections.SaveOAuthAsync(
                payload.UserId, oauth.Name, payload.Name, payload.AppId, grant.Tokens, identity, ct);
        }

        private static IServiceResult<T> Failure<T>(string code, string message)
            => ServiceResult<T>.Failure(e => e.Set(code, message));
    }
}
