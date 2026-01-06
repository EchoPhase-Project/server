using EchoPhase.Interfaces;
using Microsoft.AspNetCore.Antiforgery;

namespace EchoPhase.Security
{
    /// <summary>
    /// Provides methods for working with CSRF (Cross-Site Request Forgery) tokens,
    /// including generating, storing, retrieving, and validating tokens via headers or cookies.
    /// </summary>
    public class AntiforgeryService : IAntiforgeryService
    {
        private readonly IAntiforgery _antiforgery;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// The form field name used to submit the CSRF token in form data.
        /// </summary>
        public const string CsrfFormName = "csrf_token";

        /// <summary>
        /// The name of the cookie used to store the CSRF token.
        /// </summary>
        public const string CsrfCookieName = "XSRF-TOKEN";

        /// <summary>
        /// The name of the HTTP header used to send the CSRF token.
        /// </summary>
        public const string CsrfHeaderName = "X-CSRF-TOKEN";

        /// <summary>
        /// Initializes a new instance of the <see cref="AntiforgeryService"/> class.
        /// </summary>
        /// <param name="antiforgery">
        /// The <see cref="IAntiforgery"/> service used to generate and validate CSRF tokens.
        /// </param>
        /// <param name="httpContextAccessor">
        /// The <see cref="IHttpContextAccessor"/> used to access the current <see cref="HttpContext"/>.
        /// </param>
        public AntiforgeryService(IAntiforgery antiforgery, IHttpContextAccessor httpContextAccessor)
        {
            _antiforgery = antiforgery;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Generates and stores the CSRF token, then sets it both in the response cookie and response headers.
        /// </summary>
        /// <param name="cookieName">
        /// The name of the cookie to store the CSRF token in. Defaults to <c>CsrfCookieName</c>.
        /// </param>
        /// <param name="headerName">
        /// The name of the response header to store the CSRF token in. Defaults to <c>CsrfHeaderName</c>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the current <c>HttpContext</c> is <c>null</c>.
        /// </exception>
        public void Set(string cookieName = CsrfCookieName, string headerName = CsrfHeaderName)
        {
            SetInCookie(cookieName);
            SetInHeader(headerName);
        }

        /// <summary>
        /// Generates and stores the CSRF token, then sets it in the response headers.
        /// </summary>
        /// <param name="name">
        /// The name of the response header to store the CSRF token in. Defaults to <c>CsrfHeaderName</c>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the current <c>HttpContext</c> is <c>null</c>.
        /// </exception>
        public void SetInHeader(string name = CsrfHeaderName)
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HttpContext is null.");

            var tokens = _antiforgery.GetAndStoreTokens(httpContext);
            httpContext.Response.Headers[name] = tokens.RequestToken;
        }

        /// <summary>
        /// Generates and stores the CSRF token, then appends it to the response cookies.
        /// </summary>
        /// <param name="name">
        /// The name of the cookie in which to store the CSRF token. Defaults to <c>CsrfCookieName</c>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the current <c>HttpContext</c> is <c>null</c>.
        /// </exception>
        public void SetInCookie(string name = CsrfCookieName)
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HttpContext is null.");

            var tokens = _antiforgery.GetAndStoreTokens(httpContext);

            httpContext.Response.Cookies.Append(
                name,
                tokens.CookieToken ?? string.Empty,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = httpContext.Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Path = "/"
                });
        }

        /// <summary>
        /// Retrieves the CSRF token from the request cookies.
        /// </summary>
        /// <param name="name">
        /// The name of the cookie containing the CSRF token. Defaults to <c>CsrfCookieName</c>.
        /// </param>
        /// <returns>
        /// The CSRF token string if present in the cookies; otherwise, throws <see cref="KeyNotFoundException"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="HttpContext"/> is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown if the specified cookie is not found.</exception>
        public string GetFromCookie(string name = CsrfCookieName)
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HttpContext is null.");

            if (!httpContext.Request.Cookies.TryGetValue(name, out var token))
                throw new KeyNotFoundException($"Cookie '{name}' with token not found.");

            return token;
        }

        /// <summary>
        /// Retrieves the CSRF token from the request headers.
        /// </summary>
        /// <param name="name">
        /// The name of the header containing the CSRF token. Defaults to <c>CsrfHeaderName</c>.
        /// </param>
        /// <returns>
        /// The CSRF token string if present in the headers; otherwise, throws <see cref="KeyNotFoundException"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="HttpContext"/> is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown if the specified header or token value is not found.</exception>
        public string GetFromHeader(string name = CsrfHeaderName)
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HttpContext is null.");

            if (!httpContext.Request.Headers.TryGetValue(name, out var tokenValues))
                throw new KeyNotFoundException($"Header '{name}' with token not found.");

            return tokenValues.FirstOrDefault()
                ?? throw new KeyNotFoundException("No token values found in the header.");
        }

        /// <summary>
        /// Generates and stores a new CSRF (antiforgery) token in the current HTTP context,
        /// then returns the request token as a string.
        /// </summary>
        /// <returns>
        /// The CSRF request token string if generated successfully.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="HttpContext"/> is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown if the token value is not found or provided.</exception>
        public string Get()
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HttpContext is null.");

            var tokens = _antiforgery.GetAndStoreTokens(httpContext);
            return tokens.RequestToken
                ?? throw new KeyNotFoundException("No request token generated or found.");
        }

        /// <summary>
        /// Validates the CSRF (antiforgery) token in the current HTTP request.
        /// Throws an exception if the token is invalid or missing.
        /// </summary>
        /// <returns>A task that represents the asynchronous validation operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the HTTP context is not available.</exception>
        public async Task ValidateAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HttpContext is null.");

            await _antiforgery.ValidateRequestAsync(httpContext);
        }
    }
}
