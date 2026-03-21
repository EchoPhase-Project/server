// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Authentication.Handlers;
using EchoPhase.Security.Authentication.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EchoPhase.Security.Authentication.Extensions
{
    public static class AuthenticationBuilderExtensions
    {
        public static AuthenticationBuilder AddRefresh(
            this AuthenticationBuilder builder,
            Action<RefreshOptions>? configure = null)
        {
            return builder.AddScheme<RefreshOptions, RefreshHandler>(
                JwtBearerDefaults.AuthenticationScheme,
                configure ?? (_ => { }));
        }
    }
}
