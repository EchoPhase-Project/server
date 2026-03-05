using System.Security.Claims;
using System.Text.Encodings.Web;
using EchoPhase.Security.Authentication.Jwt;
using EchoPhase.Security.Authentication.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using EchoPhase.Security.Authentication.Jwt.Helpers;
using EchoPhase.Security.Authentication.Jwt.Providers;

namespace EchoPhase.Security.Authentication.Handlers
{
    public class RefreshHandler : AuthenticationHandler<RefreshOptions>
    {
        private readonly IJwtTokenProvider    _jwt;
        private readonly IRefreshTokenProvider _refresh;

        public RefreshHandler(
            IJwtTokenProvider      jwt,
            IRefreshTokenProvider  refresh,
            IOptionsMonitor<RefreshOptions> options,
            ILoggerFactory logger,
            UrlEncoder     encoder)
            : base(options, logger, encoder)
        {
            _jwt         = jwt;
            _refresh     = refresh;
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
                !Request.Cookies.TryGetValue(CookieNames.RefreshId,    out var refreshIdRaw) ||
                !Guid.TryParse(refreshIdRaw, out var refreshId))
            {
                return null;
            }

            try
            {
                var pair = await _refresh.RefreshAsync(refreshId, refreshToken);

                TokenCookieHelper.WriteRefreshed(Response, Request, pair);

                return await _jwt.ValidateTokenAsync(pair.AccessToken);
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
