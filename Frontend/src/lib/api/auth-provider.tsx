import {
    createContext,
    useContext,
    ParentComponent,
    createSignal,
    createResource,
    Accessor,
} from 'solid-js';
import type { User, LoginCredentials, RegisterData, ErrorItem } from '@lib/api';

interface AuthContextValue {
    user: Accessor<User | null>;
    refetchUser: Promise<User | null>;
    isAuthenticated: Accessor<boolean>;
    isLoading: Accessor<boolean>;
    csrfToken: Accessor<string | null>;
    refetchCsrf: Promise<string | null>;
    login: (
        credentials: LoginCredentials
    ) => Promise<{ success: boolean; error?: ErrorItem[] }>;
    register: (
        data: RegisterData
    ) => Promise<{ success: boolean; error?: ErrorItem[] }>;
    logout: () => Promise<void>;
    addCsrfToken: () => Record<string, string>;
    hasCsrfToken: () => boolean;
}

const AuthContext = createContext<AuthContextValue>();
const CSRF_KEY = 'X-CSRF-TOKEN';

export const AuthProvider: ParentComponent = (props) => {
    // -------------------
    // Signals & Resources
    // -------------------
    const [isLoading, setIsLoading] = createSignal(false);

    const fetchMe = async (): Promise<User | null> => {
        const res = await fetch(`/api/v1/auth/me`, {
            method: 'GET',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
        });
        if (!res.ok) return null;
        return res.json();
    };
    const [user, { refetch: refetchUser }] = createResource<User | null>(
        fetchMe
    );

    const isAuthenticated = () => !!user();

    const fetchCsrfToken = async (): Promise<string | null> => {
        sessionStorage.removeItem(CSRF_KEY);
        if (!isAuthenticated()) return null;

        try {
            const res = await fetch('/api/v1/auth/antiforgery', {
                method: 'GET',
                credentials: 'include',
            });
            if (!res.ok) return null;
            const token = res.headers.get(CSRF_KEY);
            if (token) {
                sessionStorage.setItem(CSRF_KEY, token);
                return token;
            }
            return null;
        } catch {
            return null;
        }
    };
    const [csrfToken, { refetch: refetchCsrf }] = createResource<string | null>(
        () => isAuthenticated(),
        fetchCsrfToken
    );

    // -------------------
    // CSRF Helpers
    // -------------------
    const addCsrfToken = (): Record<string, string> => {
        const t = csrfToken();
        if (!t) {
            throw new Error(
                'CSRF token is not available. Make sure to fetch it before making this request.'
            );
        }
        return { [CSRF_KEY]: t };
    };

    const hasCsrfToken = () => csrfToken() !== null;

    // -------------------
    // Auth Methods
    // -------------------
    const login = async (credentials: LoginCredentials) => {
        try {
            setIsLoading(true);
            const res = await fetch(`/api/v1/auth/login`, {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(credentials),
            });
            if (res.ok) {
                await refetchUser();
                return { success: true };
            } else {
                return {
                    success: false,
                    error: [
                        { code: 'unauthorized', description: 'Unauthorized' },
                    ],
                };
            }
        } catch {
            return {
                success: false,
                error: [
                    {
                        code: 'network_error',
                        description: 'Network error occurred',
                    },
                ],
            };
        } finally {
            setIsLoading(false);
        }
    };

    const register = async (data: RegisterData) => {
        try {
            setIsLoading(true);
            const res = await fetch(`/api/v1/auth/register`, {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data),
            });
            if (res.ok) {
                return { success: true };
            } else {
                const result: ErrorItem[] = await res.json();
                return { success: false, error: result };
            }
        } catch {
            return {
                success: false,
                error: [
                    {
                        code: 'network_error',
                        description: 'Network error occurred',
                    },
                ],
            };
        } finally {
            setIsLoading(false);
        }
    };

    const logout = async () => {
        try {
            setIsLoading(true);
            await fetch(`/api/v1/auth/logout`, {
                method: 'POST',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    ...addCsrfToken(),
                },
            });
        } catch (err) {
            console.error('Logout failed:', err);
        } finally {
            await refetchUser();
            setIsLoading(false);
        }
    };

    // -------------------
    // Context Value
    // -------------------
    const value: AuthContextValue = {
        user,
        refetchUser,
        isAuthenticated,
        isLoading,
        csrfToken,
        refetchCsrf,
        login,
        register,
        logout,
        addCsrfToken,
        hasCsrfToken,
    };

    return (
        <AuthContext.Provider value={value}>
            {props.children}
        </AuthContext.Provider>
    );
};

export const useAuth = () => {
    const context = useContext(AuthContext);
    if (!context) throw new Error('useAuth must be used within AuthProvider');
    return context;
};
