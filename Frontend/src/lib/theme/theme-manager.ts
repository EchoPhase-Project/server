import { createSignal } from 'solid-js';
import type { Theme } from '@lib/theme';

export class ThemeManager {
    private readonly getTheme: () => Theme;
    private readonly setThemeSignal: (t: Theme) => void;

    private static readonly storageKey = 'theme';
    private static mediaQuery = window.matchMedia(
        '(prefers-color-scheme: dark)'
    );
    private static systemListener: ((e: MediaQueryListEvent) => void) | null =
        null;

    constructor() {
        const [get, set] = createSignal<Theme>('light');
        this.getTheme = get;
        this.setThemeSignal = set;

        const saved = localStorage.getItem(
            ThemeManager.storageKey
        ) as Theme | null;

        if (saved === 'light' || saved === 'dark') {
            this.setThemeSignal(saved);
        } else {
            const prefersDark = ThemeManager.mediaQuery.matches;
            this.setThemeSignal(prefersDark ? 'dark' : 'light');
        }

        this.applyTheme(this.getTheme());

        if (!saved) {
            ThemeManager.systemListener = (e: MediaQueryListEvent) => {
                const systemTheme = e.matches ? 'dark' : 'light';
                this.setThemeSignal(systemTheme);
                this.applyTheme(systemTheme);
            };
            ThemeManager.mediaQuery.addEventListener(
                'change',
                ThemeManager.systemListener
            );
        }
    }

    public get theme(): () => Theme {
        return this.getTheme;
    }

    public toggle() {
        const newTheme = this.getTheme() === 'light' ? 'dark' : 'light';
        this.setTheme(newTheme);
    }

    public setTheme(theme: Theme) {
        this.setThemeSignal(theme);
        localStorage.setItem(ThemeManager.storageKey, theme);
        this.applyTheme(theme);

        if (ThemeManager.systemListener) {
            ThemeManager.mediaQuery.removeEventListener(
                'change',
                ThemeManager.systemListener
            );
            ThemeManager.systemListener = null;
        }
    }

    private applyTheme(theme: Theme) {
        document.documentElement.setAttribute('data-theme', theme);
    }
}

export const themeManager = new ThemeManager();
