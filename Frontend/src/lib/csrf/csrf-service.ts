/**
 * Service for managing CSRF tokens in session storage
 * Provides methods to save, retrieve, and clear CSRF tokens used for request authentication
 */
export class CsrfService {
    /** Session storage key for CSRF token */
    private static readonly TOKEN_KEY = 'X-CSRF-TOKEN';

    /**
     * Saves CSRF token to session storage
     * @param token - The CSRF token string to save
     * @param overwrite - Whether to overwrite existing token. Default: true
     * @returns void
     *
     * @example
     * // Overwrite existing token (default behavior)
     * CsrfService.saveToken('abc123xyz');
     *
     * @example
     * // Only save if no token exists
     * CsrfService.saveToken('abc123xyz', false);
     */
    static saveToken(token: string, overwrite: boolean = true): void {
        if (!overwrite && this.hasToken()) {
            return;
        }
        sessionStorage.setItem(this.TOKEN_KEY, token);
    }

    /**
     * Retrieves CSRF token from session storage
     * @returns The stored CSRF token or null if not found
     *
     * @example
     * const token = CsrfService.getToken();
     * if (token) {
     *   // Use token in request headers
     * }
     */
    static getToken(): string | null {
        return sessionStorage.getItem(this.TOKEN_KEY);
    }

    /**
     * Removes CSRF token from session storage
     * Typically called on logout or session expiration
     * @returns void
     *
     * @example
     * CsrfService.clearToken();
     */
    static clearToken(): void {
        sessionStorage.removeItem(this.TOKEN_KEY);
    }

    /**
     * Checks if a CSRF token exists in session storage
     * @returns true if token exists, false otherwise
     *
     * @example
     * if (CsrfService.hasToken()) {
     *   // Proceed with authenticated request
     * } else {
     *   // Request new token
     * }
     */
    static hasToken(): boolean {
        return this.getToken() !== null;
    }
}

export default CsrfService;
