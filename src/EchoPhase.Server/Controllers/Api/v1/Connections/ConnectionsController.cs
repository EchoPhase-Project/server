// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Configuration.Clients;
using EchoPhase.Connections.Models;
using EchoPhase.Connections.Services;
using EchoPhase.Security.Antiforgery.Attributes;
using EchoPhase.Security.Authentication.Extensions;
using EchoPhase.Security.Authorization.Attributes;
using EchoPhase.Security.BitMasks.Constants;
using EchoPhase.Server.Controllers.Api.v1.Common;
using EchoPhase.Server.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace EchoPhase.Server.Controllers.Api.v1.Connections
{
    [Route("api/v1/connections")]
    public class ConnectionsController : ApiControllerBase
    {
        private const string NonceCookie = "EchoPhase.ConnectionNonce";
        private const string CookiePath = "/api/v1/connections";
        private static readonly TimeSpan NonceLifetime = TimeSpan.FromMinutes(10);

        private readonly IConnectionService _connections;
        private readonly IConnectionOAuthService _oauth;
        private readonly ClientsOptions _options;

        public ConnectionsController(
            IConnectionService connections,
            IConnectionOAuthService oauth,
            IOptions<ClientsOptions> options)
        {
            _connections = connections;
            _oauth = oauth;
            _options = options.Value;
        }

        [HttpGet]
        [RequirePermission(Resources.Connections, Permissions.Read)]
        [ProducesResponseType<ConnectionResponse[]>(StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromQuery] string? provider, CancellationToken ct = default)
            => Ok((await _connections.ListAsync(User.GetUserId(), provider, ct)).Select(ConnectionResponse.From).ToList());

        /// <summary>
        /// Stores a token supplied by hand (Discord bot token, or tokens from
        /// your own Twitch app). Replaces a connection with the same name.
        /// </summary>
        [HttpPost("{provider}/manual")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Connections, Permissions.Create)]
        [RequirePrincipal(PrincipalKind.Session)]
        [ProducesResponseType<ConnectionResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> SaveManual(
            string provider, [FromBody] SaveManualConnectionRequest dto, CancellationToken ct = default)
        {
            var result = await _connections.SaveManualAsync(
                User.GetUserId(), provider,
                new ManualConnectionRequest(dto.Name, dto.AccessToken, dto.RefreshToken, dto.ClientSecret),
                ct);

            return result.Successful ? Ok(ConnectionResponse.From(result.Value)) : Failure(result.Error);
        }

        /// <summary>
        /// Starts the OAuth flow; the client navigates the browser to the returned URL.
        /// Using an existing name reconnects that connection and keeps its id.
        /// </summary>
        [HttpPost("{provider}/authorize")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Connections, Permissions.Create)]
        [RequirePrincipal(PrincipalKind.Session)]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        [ProducesResponseType<AuthorizeConnectionResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Authorize(
            string provider, [FromBody] AuthorizeConnectionRequest dto, CancellationToken ct = default)
        {
            var result = await _oauth.BeginAsync(User.GetUserId(), provider, dto.Name, dto.AppId, ct);
            if (!result.Successful)
                return Failure(result.Error);

            Response.Cookies.Append(NonceCookie, result.Value.Nonce, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                // Lax: the cookie must survive the top-level redirect back from the provider.
                SameSite = SameSiteMode.Lax,
                Path = CookiePath,
                MaxAge = NonceLifetime
            });

            return Ok(new AuthorizeConnectionResponse(result.Value.Url));
        }

        /// <summary>
        /// OAuth redirect target. Anonymous because the browser arrives from the
        /// provider without the API's bearer token; the protected state and nonce
        /// cookie identify the user instead.
        /// </summary>
        [HttpGet("{provider}/callback")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        [ProducesResponseType(StatusCodes.Status302Found)]
        public async Task<IActionResult> Callback(
            string provider,
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            CancellationToken ct = default)
        {
            var nonce = Request.Cookies[NonceCookie];
            Response.Cookies.Delete(NonceCookie, new CookieOptions { Path = CookiePath });

            if (!string.IsNullOrEmpty(error))
                return ReturnToApp("denied", error);

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return ReturnToApp("error", "missing_code");

            var result = await _oauth.CompleteAsync(provider, code, state, nonce, ct);

            return result.Successful
                ? ReturnToApp("connected", null, result.Value.Name)
                : ReturnToApp("error", result.Error.Code);
        }

        [HttpDelete("{id:guid}")]
        [ValidateAntiForgery]
        [RequirePermission(Resources.Connections, Permissions.Delete)]
        [RequirePrincipal(PrincipalKind.Session)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ErrorBody>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var result = await _connections.DeleteAsync(User.GetUserId(), id, ct);
            return result.Successful ? NoContent() : Failure(result.Error);
        }

        private IActionResult ReturnToApp(string status, string? reason, string? connection = null)
        {
            var query = new Dictionary<string, string?> { ["status"] = status };
            if (reason is not null)
                query["reason"] = reason;
            if (connection is not null)
                query["connection"] = connection;

            return Redirect(QueryHelpers.AddQueryString(_options.ConnectionsReturnUrl, query));
        }
    }

    /// <summary>Where the browser goes to sign in at the provider.</summary>
    /// <param name="Url">The provider's sign-in page, with the state of this flow.</param>
    public sealed record AuthorizeConnectionResponse(string Url);

    /// <summary>A connected account; token material never leaves the server.</summary>
    /// <param name="Id">The connection's id.</param>
    /// <param name="Provider">The service, such as <c>discord</c> or <c>twitch</c>.</param>
    /// <param name="Name">The owner's name for it; blocks refer to it by this name.</param>
    /// <param name="AuthType">How it signs in: <c>BotToken</c> (a static token) or <c>OAuth2</c>.</param>
    /// <param name="Status">Whether it works: <c>Active</c>, or a state that needs the owner (such as reconnecting).</param>
    /// <param name="OAuthAppId">The owner's OAuth app it signed in through; none: the server's app or a manual token.</param>
    /// <param name="ExternalAccountId">The account's id at the provider.</param>
    /// <param name="ExternalLogin">The account's login at the provider.</param>
    /// <param name="Scopes">The scopes the provider granted.</param>
    /// <param name="ExpiresAt">When the access token expires; none: it does not.</param>
    /// <param name="LastValidatedAt">The last time the provider accepted it.</param>
    /// <param name="LastError">The provider's last refusal, if any.</param>
    /// <param name="CreatedAt">When it was connected.</param>
    /// <param name="UpdatedAt">The last change.</param>
    public sealed record ConnectionResponse(
        Guid Id, string Provider, string Name, string AuthType, string Status, Guid? OAuthAppId,
        string? ExternalAccountId, string? ExternalLogin, IReadOnlyList<string> Scopes, DateTime? ExpiresAt,
        DateTime? LastValidatedAt, string? LastError, DateTime CreatedAt, DateTime UpdatedAt)
    {
        public static ConnectionResponse From(ConnectionSummary c) => new(
            c.Id, c.Provider, c.Name, c.AuthType, c.Status, c.OAuthAppId, c.ExternalAccountId, c.ExternalLogin,
            c.Scopes, c.ExpiresAt, c.LastValidatedAt, c.LastError, c.CreatedAt, c.UpdatedAt);
    }
}
