// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using AesGcm = EchoPhase.Security.Cryptography.AesGcm;

namespace EchoPhase.Security.Antiforgery
{
    public class AntiforgeryService : IAntiforgeryService
    {
        private readonly AesGcm _aes;
        private readonly IHttpContextAccessor _httpContext;

        public const string CookieName = "XSRF-TOKEN";
        public const string HeaderName = "X-CSRF-TOKEN";

        public AntiforgeryService(AesGcm aes, IHttpContextAccessor httpContext)
        {
            _aes = aes;
            _httpContext = httpContext;
        }

        public void Set()
        {
            var context = GetContext();
            var userId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";

            var binding = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            var cookiePayload = new CsrfPayload(UserId: userId, Random: binding + ":cookie");
            var headerPayload = new CsrfPayload(UserId: userId, Random: binding + ":header");

            var cookieToken = ToUrlSafeBase64(_aes.EncryptToBase64(JsonSerializer.Serialize(cookiePayload)));
            var headerToken = ToUrlSafeBase64(_aes.EncryptToBase64(JsonSerializer.Serialize(headerPayload)));

            context.Response.Cookies.Append(CookieName, cookieToken, BuildCookieOptions(context));
            context.Response.Headers[HeaderName] = headerToken;
        }

        public void Remove()
        {
            var context = GetContext();
            context.Response.Cookies.Delete(CookieName, BuildCookieOptions(context));
        }

        public Task ValidateAsync()
        {
            var context = GetContext();

            if (!context.Request.Cookies.TryGetValue(CookieName, out var cookie) ||
                string.IsNullOrEmpty(cookie))
                throw new InvalidOperationException("CSRF cookie missing.");

            var header = context.Request.Headers[HeaderName].FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                throw new InvalidOperationException("CSRF header missing.");

            if (string.Equals(cookie, header, StringComparison.Ordinal))
                throw new InvalidOperationException("CSRF tokens must differ.");

            CsrfPayload cookiePayload;
            CsrfPayload headerPayload;
            try
            {
                cookiePayload = Decrypt(cookie);
                headerPayload = Decrypt(header);
            }
            catch
            {
                throw new InvalidOperationException("CSRF token tampered or invalid.");
            }

            var cookieBinding = cookiePayload.Random.Replace(":cookie", "");
            var headerBinding = headerPayload.Random.Replace(":header", "");

            if (cookieBinding != headerBinding)
                throw new InvalidOperationException("CSRF token binding mismatch.");

            if (cookiePayload.Random == headerPayload.Random)
                throw new InvalidOperationException("CSRF tokens must differ.");

            if (cookiePayload.UserId != headerPayload.UserId)
                throw new InvalidOperationException("CSRF token user mismatch.");

            var currentUserId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? "anonymous";
            if (cookiePayload.UserId != currentUserId)
                throw new InvalidOperationException("CSRF token user mismatch.");

            return Task.CompletedTask;
        }

        private CsrfPayload Decrypt(string token)
        {
            var normalized = FromUrlSafeBase64(token);
            var json = Encoding.UTF8.GetString(_aes.Decrypt(normalized));
            return JsonSerializer.Deserialize<CsrfPayload>(json)
                   ?? throw new InvalidOperationException("Invalid CSRF token.");
        }

        private static string ToUrlSafeBase64(string base64) =>
            base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string FromUrlSafeBase64(string urlSafe) =>
            urlSafe.Replace('-', '+').Replace('_', '/')
            + new string('=', (4 - urlSafe.Length % 4) % 4);

        private HttpContext GetContext() =>
            _httpContext.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available.");

        private static CookieOptions BuildCookieOptions(HttpContext context) => new()
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true
        };

        private record CsrfPayload(string UserId, string Random);
    }
}
