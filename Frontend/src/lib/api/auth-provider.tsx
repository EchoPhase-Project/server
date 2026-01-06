import {
    createContext,
    useContext,
    ParentComponent,
    createSignal,
    createEffect,
    Accessor,
} from 'solid-js';
import { API_CONFIG } from '@lib/api';
import type { User, LoginCredentials, RegisterData, ErrorItem } from '@lib/api';
import { CsrfService } from '@lib/csrf';

interface AuthContextValue {
    user: Accessor<User | null>;
    isAuthenticated: Accessor<boolean>;
    isLoading: Accessor<boolean>;
    login: (credentials: LoginCredentials) => Promise<{
        success: boolean;
        error?: ErrorItem[];
    }>;
    register: (data: RegisterData) => Promise<{
        success: boolean;
        error?: ErrorItem[];
    }>;
    logout: () => Promise<void>;
    checkAuth: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue>();

export const AuthProvider: ParentComponent = (props) => {
    const [user, setUser] = createSignal<User | null>(null);
    const [isLoading, setIsLoading] = createSignal(false);
    const [isInitialized, setIsInitialized] = createSignal(false);

    const isAuthenticated = () => user() !== null;

    createEffect(() => {
        if (!isInitialized()) {
            checkAuth();
        }
    });

    const checkAuth = async () => {
        try {
            setIsLoading(true);
            const res = await fetch(`/api/v1/auth/me`, {
                method: 'GET',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
            });

            if (res.ok) {
                const userData: User = await res.json();
                setUser(userData);
            } else {
                setUser(null);
            }
        } catch (error) {
            console.error('Auth check failed:', error);
            setUser(null);
        } finally {
            setIsLoading(false);
            setIsInitialized(true);
        }
    };

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
                const csrfToken = res.headers.get('X-CSRF-TOKEN');
                if (csrfToken) {
                    CsrfService.saveToken(csrfToken);
                    localStorage.setItem('csrf-token', csrfToken);
                }

                const userData: User = await res.json();
                setUser(userData);

                return { success: true };
            } else {
                return {
                    success: false,
                    error: [
                        { code: 'unauthorized', description: 'Unauthorized' },
                    ],
                };
            }
        } catch (error) {
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
                const userData: User = await res.json();
                setUser(userData);

                return { success: true };
            } else {
                const result: ErrorItem[] = await res.json();
                return {
                    success: false,
                    error: result,
                };
            }
        } catch (error) {
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
                headers: { 'Content-Type': 'application/json' },
            });
        } catch (error) {
            console.error('Logout failed:', error);
        } finally {
            setUser(null);
            localStorage.removeItem('csrf-token');
            setIsLoading(false);
        }
    };

    const value: AuthContextValue = {
        user,
        isAuthenticated,
        isLoading,
        login,
        register,
        logout,
        checkAuth,
    };

    return (
        <AuthContext.Provider value={value}>
            {props.children}
        </AuthContext.Provider>
    );
};

export const useAuth = () => {
    const context = useContext(AuthContext);
    if (!context) {
        throw new Error('useAuth must be used within AuthProvider');
    }
    return context;
};
