using EchoPhase.Services;

namespace EchoPhase.Interfaces
{
    public interface IAntiforgeryService
    {
        /// <summary>
        /// Generates and stores the CSRF token, then sets it in the response cookie.
        /// </summary>
        /// <param name="name">The name of the cookie to store the CSRF token. Optional.</param>
        void SetInCookie(string name = AntiforgeryService.CsrfCookieName);

        /// <summary>
        /// Generates and stores the CSRF token, then sets it in the response headers.
        /// </summary>
        /// <param name="name">The name of the header to store the CSRF token. Optional.</param>
        void SetInHeader(string name = AntiforgeryService.CsrfHeaderName);

        /// <summary>
        /// Generates and stores the CSRF token, then sets it both in cookie and header.
        /// </summary>
        /// <param name="cookieName">The cookie name to store the CSRF token. Optional.</param>
        /// <param name="headerName">The header name to store the CSRF token. Optional.</param>
        void Set(string cookieName = AntiforgeryService.CsrfCookieName, string headerName = AntiforgeryService.CsrfHeaderName);

        /// <summary>
        /// Retrieves the current CSRF token from the request context.
        /// </summary>
        /// <returns>The CSRF token string or null if unavailable.</returns>
        string Get();

        /// <summary>
        /// Retrieves the CSRF token value from the specified request cookie.
        /// </summary>
        /// <param name="name">The cookie name to look for. Optional.</param>
        /// <returns>The CSRF token string or null if unavailable.</returns>
        string GetFromCookie(string name = AntiforgeryService.CsrfCookieName);

        /// <summary>
        /// Retrieves the CSRF token value from the specified request header.
        /// </summary>
        /// <param name="name">The header name to look for. Optional.</param>
        /// <returns>The CSRF token string or null if unavailable.</returns>
        string GetFromHeader(string name = AntiforgeryService.CsrfHeaderName);

        /// <summary>
        /// Validates the CSRF token on the current request.
        /// </summary>
        /// <returns>A task representing the asynchronous validation operation.</returns>
        Task ValidateAsync();
    }
}
