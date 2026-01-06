import {
    createContext,
    useContext,
    ParentComponent,
    createSignal,
    createEffect,
    onCleanup,
} from 'solid-js';
import type { Theme } from '@lib/theme';

type ThemeContextValue = [() => Theme, (t: Theme) => void];

const ThemeContext = createContext<ThemeContextValue>();

export const ThemeProvider: ParentComponent = (props) => {
    const [theme, setTheme] = createSignal<Theme>('light');

    const saved = localStorage.getItem('theme') as Theme | null;

    if (saved === 'light' || saved === 'dark') {
        setTheme(saved);
    } else {
        const prefersDark = window.matchMedia(
            '(prefers-color-scheme: dark)'
        ).matches;
        setTheme(prefersDark ? 'dark' : 'light');
    }

    createEffect(() => {
        const t = theme();
        document.documentElement.setAttribute('data-theme', t);
        localStorage.setItem('theme', t);
    });

    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const listener = (e: MediaQueryListEvent) => {
        if (!saved) {
            setTheme(e.matches ? 'dark' : 'light');
        }
    };
    mediaQuery.addEventListener('change', listener);

    onCleanup(() => {
        mediaQuery.removeEventListener('change', listener);
    });

    return (
        <ThemeContext.Provider value={[theme, setTheme]}>
            {props.children}
        </ThemeContext.Provider>
    );
};

export function useTheme() {
    const ctx = useContext(ThemeContext);
    if (!ctx) throw new Error('useTheme must be used within a ThemeProvider');
    return ctx;
}
