using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using EchoPhase.Security.Authentication.Options;
using EchoPhase.Security.Authentication.Handlers;

namespace EchoPhase.Security.Authentication.Extensions
{
    public static class CustomJwtBearerExtensions
    {
        public static AuthenticationBuilder AddCustomJwtBearer(
            this AuthenticationBuilder builder,
            Action<CustomJwtBearerOptions>? configure = null)
        {
            return builder.AddScheme<CustomJwtBearerOptions, CustomJwtBearerHandler>(
                JwtBearerDefaults.AuthenticationScheme,
                configure ?? (_ => { }));
        }
    }
}
