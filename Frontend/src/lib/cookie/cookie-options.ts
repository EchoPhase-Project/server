export type CookieOptions = {
    expires?: Date | string;
    path?: string;
    domain?: string;
    secure?: boolean;
    sameSite?: 'Strict' | 'Lax' | 'None';
};

/** Default cookie options used if not overridden */
export const defaultCookieOptions: Required<Omit<CookieOptions, 'httpOnly'>> = {
    expires: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000), // 7 days from now
    path: '/',
    domain: window.location.hostname,
    secure: true,
    sameSite: 'Strict',
};

export default CookieOptions;
