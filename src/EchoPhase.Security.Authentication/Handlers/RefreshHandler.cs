// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Claims;
using System.Text.Encodings.Web;
using EchoPhase.Security.Antiforgery;
using EchoPhase.Security.Authentication.Jwt;
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
            var fromHeader = false;

            var authorization = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authorization) &&
                authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authorization["Bearer ".Length..].Trim();
                fromHeader = true;
            }

            token ??= Request.Cookies[CookieNames.AccessToken];

            var messageReceivedContext = new MessageReceivedContext(Context, Scheme, Options)
            {
                Token = token
            };
            await Options.Events.MessageReceived(messageReceivedContext);
            if (messageReceivedContext.Result is not null)
                return messageReceivedContext.Result;
            if (!string.IsNullOrEmpty(messageReceivedContext.Token)
                && messageReceivedContext.Token != token)
            {
                token = messageReceivedContext.Token;
                fromHeader = false;
            }

            ClaimsPrincipal? principal = null;

            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    principal = await _jwt.ValidateTokenAsync(token, Context.RequestAborted);
                }
                catch (SecurityTokenExpiredException)
                {
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
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
                // A client that sends its own token manages it; falling back to
                // cookies would silently switch identities (e.g. an expired
                // impersonation token turning into the actor's own session).
                if (fromHeader)
                    return AuthenticateResult.NoResult();

                // Cookie fallback: ambient credentials, so CSRF checks still apply.
                principal = await TrySilentRefreshAsync();

                if (principal is null)
                    return AuthenticateResult.NoResult();
            }
            else if (fromHeader)
            {
                CsrfExemption.MarkHeaderAuthenticated(Context);
            }

            var tokenValidatedContext = new TokenValidatedContext(Context, Scheme, Options)
            {
                Principal = principal
            };
            await Options.Events.TokenValidated(tokenValidatedContext);
            if (tokenValidatedContext.Result is not null)
                return tokenValidatedContext.Result;

            tokenValidatedContext.Success();
            return tokenValidatedContext.Result
                   ?? throw new InvalidOperationException("A validated token gave no authentication result.");
        }

        private async Task<ClaimsPrincipal?> TrySilentRefreshAsync()
        {
            if (!Request.Cookies.TryGetValue(CookieNames.RefreshToken, out var refreshToken)
                || string.IsNullOrEmpty(refreshToken))
                return null;

            try
            {
                var result = await _signInManager.RefreshAsync(refreshToken, Context.RequestAborted);
                if (!result.Successful)
                {
                    // Unknown, reused or revoked token: the session is gone for good.
                    TokenCookieHelper.Clear(Response);
                    return null;
                }

                var pair = result.Value;
                TokenCookieHelper.WriteRefreshed(Response, Request, pair);

                return await _jwt.ValidateTokenAsync(pair.AccessToken, Context.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Infrastructure failure (database, etc.): keep the cookies so the
                // next request can retry instead of logging the user out.
                Logger.LogWarning(ex, "Silent session refresh failed.");
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
