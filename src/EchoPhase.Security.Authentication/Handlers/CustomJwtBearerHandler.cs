using EchoPhase.Security.Authentication.Jwt;
using EchoPhase.Security.Authentication.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Text.Encodings.Web;

namespace EchoPhase.Security.Authentication.Handlers
{
    public class CustomJwtBearerHandler : AuthenticationHandler<CustomJwtBearerOptions>
    {
        private readonly IJwtTokenProvider _jwt;

        public CustomJwtBearerHandler(
            IJwtTokenProvider jwt,
            IOptionsMonitor<CustomJwtBearerOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
            _jwt = jwt;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authorization = Request.Headers.Authorization.FirstOrDefault();

            if (string.IsNullOrEmpty(authorization))
                return AuthenticateResult.NoResult();

            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return AuthenticateResult.NoResult();

            var token = authorization["Bearer ".Length..].Trim();

            if (string.IsNullOrEmpty(token))
                return AuthenticateResult.NoResult();

            var messageReceivedContext = new MessageReceivedContext(Context, Scheme, Options)
            {
                Token = token
            };

            await Options.Events.MessageReceived(messageReceivedContext);

            if (messageReceivedContext.Result is not null)
                return messageReceivedContext.Result;

            if (!string.IsNullOrEmpty(messageReceivedContext.Token))
                token = messageReceivedContext.Token;

            ClaimsPrincipal? principal;

            try
            {
                principal = await _jwt.ValidateTokenAsync(token);
            }
            catch (Exception ex)
            {
                var authenticationFailedContext = new AuthenticationFailedContext(Context, Scheme, Options)
                {
                    Exception = ex
                };

                await Options.Events.AuthenticationFailed(authenticationFailedContext);

                if (authenticationFailedContext.Result is not null)
                    return authenticationFailedContext.Result;

                return AuthenticateResult.Fail(ex);
            }

            if (principal is null)
                return AuthenticateResult.Fail("Invalid token");

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

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            var authResult = await HandleAuthenticateOnceSafeAsync();

            var challengeContext = new JwtBearerChallengeContext(Context, Scheme, Options, properties)
            {
                AuthenticateFailure = authResult.Failure
            };

            await Options.Events.Challenge(challengeContext);

            if (challengeContext.Handled)
                return;

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
