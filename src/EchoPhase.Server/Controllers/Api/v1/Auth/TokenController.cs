// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using System.Text.Json.Serialization;
using EchoPhase.Security.Authentication.ApiClients;
using EchoPhase.Server.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace EchoPhase.Server.Controllers.Api.v1.Auth
{
    /// <summary>
    /// OAuth 2.0 token endpoint for the client-credentials grant (RFC 6749 §4.4).
    /// </summary>
    [Route("api/v1/auth/token")]
    public class TokenController : ApiControllerBase
    {
        private const string ClientCredentials = "client_credentials";

        private readonly IApiClientService _clients;

        public TokenController(IApiClientService clients)
        {
            _clients = clients;
        }

        /// <summary>
        /// Exchanges client credentials for a short-lived access token. Credentials
        /// may be sent with HTTP Basic auth or as <c>client_id</c>/<c>client_secret</c>
        /// form fields. No refresh token is issued: request a new token when it expires.
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [Consumes("application/x-www-form-urlencoded")]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        [ProducesResponseType<ClientTokenResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<OAuthErrorResponse>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<OAuthErrorResponse>(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Token(
            [FromForm(Name = "grant_type")] string? grantType,
            [FromForm(Name = "client_id")] string? clientIdField,
            [FromForm(Name = "client_secret")] string? clientSecretField,
            CancellationToken ct = default)
        {
            // Tokens and errors must not be cached (RFC 6749 §5.1).
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";

            if (grantType != ClientCredentials)
                return OAuthError(StatusCodes.Status400BadRequest, "unsupported_grant_type");

            var basic = ReadBasicCredentials();
            if (basic is not null && (clientIdField is not null || clientSecretField is not null))
                return OAuthError(StatusCodes.Status400BadRequest, "invalid_request",
                    "Use either HTTP Basic auth or form fields, not both.");

            var (rawId, secret) = basic ?? (clientIdField, clientSecretField);

            if (!Guid.TryParse(rawId, out var clientId) || string.IsNullOrEmpty(secret))
                return InvalidClient(basic is not null);

            var result = await _clients.IssueTokenAsync(clientId, secret, ct);
            if (!result.Successful)
                return InvalidClient(basic is not null);

            return Ok(new ClientTokenResponse(result.Value.AccessToken, "Bearer", result.Value.ExpiresIn));
        }

        private (string? Id, string? Secret)? ReadBasicCredentials()
        {
            var header = Request.Headers.Authorization.FirstOrDefault();
            if (header is null || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
                var separator = decoded.IndexOf(':');
                if (separator < 0)
                    return (null, null);

                // RFC 6749 §2.3.1: both parts are form-urlencoded before encoding.
                return (Uri.UnescapeDataString(decoded[..separator]),
                        Uri.UnescapeDataString(decoded[(separator + 1)..]));
            }
            catch (FormatException)
            {
                return (null, null);
            }
        }

        private IActionResult InvalidClient(bool usedBasic)
        {
            if (usedBasic)
                Response.Headers[HeaderNames.WWWAuthenticate] = "Basic realm=\"EchoPhase\"";

            return OAuthError(StatusCodes.Status401Unauthorized, "invalid_client");
        }

        private ObjectResult OAuthError(int status, string error, string? description = null)
            => StatusCode(status, new OAuthErrorResponse(error, description));
    }

    /// <summary>An access token for an API client (RFC 6749 §5.1; the field names are OAuth's).</summary>
    /// <param name="AccessToken">The token; send it as <c>Authorization: Bearer</c>.</param>
    /// <param name="TokenType">Always <c>Bearer</c>.</param>
    /// <param name="ExpiresIn">Seconds until it expires.</param>
    public sealed record ClientTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    /// <summary>An OAuth error (RFC 6749 §5.2).</summary>
    /// <param name="Error">The error code, such as <c>invalid_client</c>.</param>
    /// <param name="ErrorDescription">A text for the developer; often none.</param>
    public sealed record OAuthErrorResponse(
        [property: JsonPropertyName("error")] string Error,
        [property: JsonPropertyName("error_description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ErrorDescription = null);
}
