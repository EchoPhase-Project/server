using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace EchoPhase.Security.Antiforgery
{
    /// <summary>
    /// Provides methods for working with CSRF (Cross-Site Request Forgery) tokens,
    /// including generating, storing, retrieving, and validating tokens via headers or cookies.
    /// </summary>
    public class AntiforgeryService : IAntiforgeryService
    {
        private readonly IAntiforgery _antiforgery;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public const string CsrfFormName = "csrf_token";
        public const string CsrfCookieName = "XSRF-TOKEN";
        public const string CsrfHeaderName = "X-CSRF-TOKEN";

        public AntiforgeryService(IAntiforgery antiforgery, IHttpContextAccessor httpContextAccessor)
        {
            _antiforgery = antiforgery ?? throw new ArgumentNullException(nameof(antiforgery));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        /// <summary>
        /// Generates and stores the CSRF token, then sets it both in the response cookie and response headers.
        /// </summary>
        public void Set(string cookieName = CsrfCookieName, string headerName = CsrfHeaderName)
        {
            var httpContext = GetHttpContext();
            var tokens = _antiforgery.GetAndStoreTokens(httpContext);

            if (string.IsNullOrEmpty(tokens.RequestToken))
                throw new InvalidOperationException("Request token is null or empty.");

            httpContext.Response.Headers[headerName] = tokens.RequestToken;

            if (!string.IsNullOrEmpty(tokens.CookieToken))
            {
                httpContext.Response.Cookies.Append(
                    cookieName,
                    tokens.CookieToken,
                    BuildCookieOptions(httpContext));
            }
        }

        /// <summary>
        /// Retrieves the CSRF token from the request cookies.
        /// </summary>
        public string GetFromCookie(string name = CsrfCookieName)
        {
            var httpContext = GetHttpContext();

            if (!httpContext.Request.Cookies.TryGetValue(name, out var token))
                throw new KeyNotFoundException($"Cookie '{name}' with token not found.");

            return token;
        }

        /// <summary>
        /// Retrieves the CSRF token from the request headers.
        /// </summary>
        public string GetFromHeader(string name = CsrfHeaderName)
        {
            var httpContext = GetHttpContext();

            if (!httpContext.Request.Headers.TryGetValue(name, out var tokenValues))
                throw new KeyNotFoundException($"Header '{name}' with token not found.");

            return tokenValues.FirstOrDefault()
                ?? throw new KeyNotFoundException("No token values found in the header.");
        }

        /// <summary>
        /// Generates and stores a new CSRF token set in the current HTTP context.
        /// </summary>
        public AntiforgeryTokenSet Get()
        {
            var httpContext = GetHttpContext();

            return _antiforgery.GetAndStoreTokens(httpContext)
                ?? throw new InvalidOperationException("Failed to generate antiforgery token set.");
        }

        /// <summary>
        /// Validates the CSRF token in the current HTTP request.
        /// </summary>
        public async Task ValidateAsync()
        {
            var httpContext = GetHttpContext();
            await _antiforgery.ValidateRequestAsync(httpContext);
        }

        /// <summary>
        /// Removes the CSRF token cookie from the response.
        /// </summary>
        public void Remove(string cookieName = CsrfCookieName)
        {
            var httpContext = GetHttpContext();
            httpContext.Response.Cookies.Delete(cookieName, BuildCookieOptions(httpContext));
        }

        // -------------------------
        // Private helpers
        // -------------------------

        private HttpContext GetHttpContext() =>
            _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HttpContext is not available.");

        private static CookieOptions BuildCookieOptions(HttpContext httpContext) => new()
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true
        };
    }
}
