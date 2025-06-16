export class CsrfService {
    private static readonly TOKEN_KEY = 'X-CSRF-TOKEN';

    static saveToken(token: string): void {
        sessionStorage.setItem(this.TOKEN_KEY, token);
    }

    static getToken(): string | null {
        return sessionStorage.getItem(this.TOKEN_KEY);
    }

    static clearToken(): void {
        sessionStorage.removeItem(this.TOKEN_KEY);
    }
}

export default CsrfService;
