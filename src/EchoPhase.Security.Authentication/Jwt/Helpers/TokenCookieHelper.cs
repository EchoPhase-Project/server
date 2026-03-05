using Microsoft.AspNetCore.Http;
using EchoPhase.Security.Authentication.Jwt.Providers;

namespace EchoPhase.Security.Authentication.Jwt.Helpers
{
    public static class TokenCookieHelper
    {
        private static CookieOptions Session(HttpRequest request) => new()
        {
            HttpOnly = true,
            Secure   = request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path     = "/"
        };

        private static CookieOptions LongLived(HttpRequest request) => new()
        {
            HttpOnly = true,
            Secure   = request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path     = "/",
            Expires  = DateTimeOffset.UtcNow.AddDays(400)
        };

        public static void WriteInitial(HttpResponse response, HttpRequest request, TokenInitial initial)
        {
            response.Cookies.Append(CookieNames.AccessToken,  initial.Tokens.AccessToken,  Session(request));
            response.Cookies.Append(CookieNames.RefreshToken, initial.Tokens.RefreshToken, LongLived(request));
            response.Cookies.Append(CookieNames.RefreshId,    initial.Id.ToString(),        LongLived(request));
        }

        public static void WriteRefreshed(HttpResponse response, HttpRequest request, TokenPair pair)
        {
            response.Cookies.Append(CookieNames.AccessToken,  pair.AccessToken,  Session(request));
            response.Cookies.Append(CookieNames.RefreshToken, pair.RefreshToken, LongLived(request));
            if (request.Cookies.TryGetValue(CookieNames.RefreshId, out var refreshId))
                response.Cookies.Append(CookieNames.RefreshId, refreshId, LongLived(request));
        }

        public static void Clear(HttpResponse response)
        {
            response.Cookies.Delete(CookieNames.AccessToken);
            response.Cookies.Delete(CookieNames.RefreshToken);
            response.Cookies.Delete(CookieNames.RefreshId);
        }
    }
}
