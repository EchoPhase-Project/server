// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using System.Text.Encodings.Web;
using EchoPhase.Security.Authentication.Jwt;
using EchoPhase.Security.Authentication.Jwt.Exceptions;
using EchoPhase.Security.Authentication.Jwt.Helpers;
using EchoPhase.Security.Authentication.Jwt.Providers;
using EchoPhase.Security.Authentication.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EchoPhase.Security.Authentication.Handlers
{
    public class RefreshHandler : AuthenticationHandler<RefreshOptions>
    {
        private readonly IJwtTokenProvider _jwt;
        private readonly RefreshSignInManager _signInManager;

        public RefreshHandler(
            IJwtTokenProvider jwt,
            RefreshSignInManager signInManager,
            IOptionsMonitor<RefreshOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
            _jwt = jwt;
            _signInManager = signInManager;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? token = null;
            token ??= Request.Cookies[CookieNames.AccessToken];

            var authorization = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authorization) &&
                authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authorization["Bearer ".Length..].Trim();
            }

            token ??= Request.Cookies[CookieNames.AccessToken];

            var messageReceivedContext = new MessageReceivedContext(Context, Scheme, Options)
            {
                Token = token
            };
            await Options.Events.MessageReceived(messageReceivedContext);
            if (messageReceivedContext.Result is not null)
                return messageReceivedContext.Result;
            if (!string.IsNullOrEmpty(messageReceivedContext.Token))
                token = messageReceivedContext.Token;

            ClaimsPrincipal? principal = null;

            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    principal = await _jwt.ValidateTokenAsync(token);
                }
                catch (SecurityTokenExpiredException)
                {
                }
                catch (Exception ex)
                {
                    var failedCtx = new AuthenticationFailedContext(Context, Scheme, Options)
                    {
                        Exception = ex
                    };
                    await Options.Events.AuthenticationFailed(failedCtx);
                    return failedCtx.Result ?? AuthenticateResult.Fail(ex);
                }
            }

            if (principal is null)
            {
                principal = await TrySilentRefreshAsync();

                if (principal is null)
                    return AuthenticateResult.NoResult();
            }

            var tokenValidatedContext = new TokenValidatedContext(Context, Scheme, Options)
            {
                Principal = principal
            };
            await Options.Events.TokenValidated(tokenValidatedContext);
            if (tokenValidatedContext.Result is not null)
                return tokenValidatedContext.Result;

            tokenValidatedContext.Success();
            return tokenValidatedContext.Result!;
        }

        private async Task<ClaimsPrincipal?> TrySilentRefreshAsync()
        {
            if (!Request.Cookies.TryGetValue(CookieNames.RefreshToken, out var refreshToken) ||
                !Request.Cookies.TryGetValue(CookieNames.RefreshId, out var refreshIdRaw) ||
                !Guid.TryParse(refreshIdRaw, out var refreshId))
            {
                return null;
            }

            try
            {
                var pair = await _signInManager.RefreshAsync(refreshId, refreshToken);

                TokenCookieHelper.WriteRefreshed(Response, Request, pair);

                return await _jwt.ValidateTokenAsync(pair.AccessToken);
            }
            catch (RefreshTokenReusedException)
            {
                TokenCookieHelper.Clear(Response);
                return null;
            }
            catch
            {
                TokenCookieHelper.Clear(Response);
                return null;
            }
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            var authResult = await HandleAuthenticateOnceSafeAsync();
            var challengeContext = new JwtBearerChallengeContext(Context, Scheme, Options, properties)
            {
                AuthenticateFailure = authResult.Failure
            };
            await Options.Events.Challenge(challengeContext);
            if (challengeContext.Handled) return;

            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.Headers.Append("WWW-Authenticate", "Bearer");
        }

        protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            var forbiddenContext = new ForbiddenContext(Context, Scheme, Options);
            await Options.Events.Forbidden(forbiddenContext);
            Response.StatusCode = StatusCodes.Status403Forbidden;
        }
    }
}
