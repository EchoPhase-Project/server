// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Security.Authentication.Jwt.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Security.Authentication.Jwt.Helpers
{
    public static class TokenCookieHelper
    {
        private static CookieOptions Session(HttpRequest request) => new()
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        };

        private static CookieOptions LongLived(HttpRequest request) => new()
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = (request.HttpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow().AddDays(400)
        };

        public static void WriteInitial(HttpResponse response, HttpRequest request, TokenInitial initial)
        {
            response.Cookies.Append(CookieNames.AccessToken, initial.Tokens.AccessToken, Session(request));
            response.Cookies.Append(CookieNames.RefreshToken,
                initial.Tokens.RefreshToken ?? throw new InvalidOperationException("A sign-in issued no refresh token."),
                LongLived(request));
        }

        public static void WriteRefreshed(HttpResponse response, HttpRequest request, TokenPair pair)
        {
            response.Cookies.Append(CookieNames.AccessToken, pair.AccessToken, Session(request));

            // No new refresh token means a parallel request rotated it; leave the
            // cookie that request set untouched.
            if (pair.RefreshToken is not null)
                response.Cookies.Append(CookieNames.RefreshToken, pair.RefreshToken, LongLived(request));
        }

        public static void Clear(HttpResponse response)
        {
            response.Cookies.Delete(CookieNames.AccessToken);
            response.Cookies.Delete(CookieNames.RefreshToken);
        }
    }
}
