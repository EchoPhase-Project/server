import {
    createContext,
    useContext,
    createSignal,
    onMount,
    JSX,
    createMemo,
} from 'solid-js';

export type Translations = Record<string, string>;

interface LocaleDefinition {
    nativeName: string;
    translations?: Translations;
}

interface I18nContextType {
    currentLocale: () => string;
    translations: () => Translations;
    setLocale: (locale: string) => Promise<void>;
    _: (key: string, fallback?: string) => string;
    t: (key: string, fallback?: string) => string; // Alias for _
    getAvailableLocales: () => { code: string; nativeName: string }[];
    isLoading: () => boolean;
}

const defaultLocale = 'en';
const STORAGE_KEY = 'app_locale';

const I18nContext = createContext<I18nContextType>();

/**
 * Hook to access i18n functionality
 * @throws Error if used outside I18nProvider
 * @returns I18n context with translation methods
 *
 * @example
 * const { _, currentLocale, setLocale } = useI18n();
 * const greeting = _('hello', 'Hello');
 */
export function useI18n() {
    const ctx = useContext(I18nContext);
    if (!ctx) throw new Error('useI18n must be used within I18nProvider');
    return ctx;
}

/**
 * I18n Provider component for internationalization support
 * Automatically detects browser locale and loads translations
 *
 * @example
 * <I18nProvider>
 *   <App />
 * </I18nProvider>
 */
export function I18nProvider(props: { children: JSX.Element }) {
    // Configuration: available locales
    const locales: Record<string, LocaleDefinition> = {
        en: { nativeName: 'English' },
        ro: { nativeName: 'Română' },
        ru: { nativeName: 'Русский' },
    };

    // State
    const [currentLocale, setCurrentLocale] =
        createSignal<string>(defaultLocale);
    const [translations, setTranslations] = createSignal<Translations>({});
    const [isLoading, setIsLoading] = createSignal<boolean>(true);

    /**
     * Detects browser's preferred locale
     * @returns Two-letter locale code or null
     */
    function getBrowserLocale(): string | null {
        if (typeof navigator !== 'undefined') {
            const lang =
                navigator.language ||
                (navigator as Navigator & { userLanguage?: string })
                    .userLanguage;
            return lang?.slice(0, 2).toLowerCase() ?? null;
        }
        return null;
    }

    /**
     * Determines initial locale from: localStorage → browser → default
     * @returns Valid locale code
     */
    function getInitialLocale(): string {
        // 1. Check localStorage
        if (typeof localStorage !== 'undefined') {
            const stored = localStorage.getItem(STORAGE_KEY);
            if (stored && locales[stored]) {
                return stored;
            }
        }

        // 2. Check browser locale
        const browserLocale = getBrowserLocale();
        if (browserLocale && locales[browserLocale]) {
            return browserLocale;
        }

        // 3. Fallback to default
        return defaultLocale;
    }

    /**
     * Loads translation file for specified locale
     * @param locale - Locale code to load
     */
    async function loadTranslations(locale: string): Promise<void> {
        // Validate locale
        if (!locales[locale]) {
            console.warn(
                `Locale "${locale}" not supported, fallback to '${defaultLocale}'`
            );
            locale = defaultLocale;
        }

        // Skip if already loaded
        if (locales[locale].translations && locale === currentLocale()) {
            setIsLoading(false);
            return;
        }

        setIsLoading(true);

        try {
            const raw = await import(`@static/locales/${locale}.json?raw`);
            const loaded: Translations = JSON.parse(raw.default);

            // Cache translations
            locales[locale].translations = loaded;

            setTranslations(loaded);
            setCurrentLocale(locale);

            // Persist to localStorage
            if (typeof localStorage !== 'undefined') {
                localStorage.setItem(STORAGE_KEY, locale);
            }

            // Update HTML lang attribute for accessibility
            if (typeof document !== 'undefined') {
                document.documentElement.lang = locale;
            }
        } catch (e) {
            console.error(`Error loading locale "${locale}":`, e);
        } finally {
            setIsLoading(false);
        }
    }

    /**
     * Changes current locale and loads its translations
     * @param locale - Target locale code
     */
    async function setLocale(locale: string): Promise<void> {
        if (locales[locale] && locale !== currentLocale()) {
            await loadTranslations(locale);
        }
    }

    /**
     * Translates a key to current locale
     * @param key - Translation key
     * @param fallback - Optional fallback text (defaults to key)
     * @returns Translated string or fallback
     *
     * @example
     * _('greeting.hello') // Returns translation
     * _('missing.key', 'Default text') // Returns 'Default text'
     */
    function _(key: string, fallback?: string): string {
        const parts = key.split('.');
        let current = translations();

        for (const part of parts) {
            if (
                current == null ||
                typeof current !== 'object' ||
                !(part in current)
            ) {
                return fallback ?? key;
            }
            current = current[part];
        }

        return typeof current === 'string' ? current : (fallback ?? key);
    }

    /**
     * Gets list of available locales with their native names
     * @returns Array of locale objects
     */
    const getAvailableLocales = createMemo(() => {
        return Object.entries(locales).map(([code, def]) => ({
            code,
            nativeName: def.nativeName,
        }));
    });

    // Initialize on mount
    onMount(() => {
        const initial = getInitialLocale();
        loadTranslations(initial);
    });

    return (
        <I18nContext.Provider
            value={{
                currentLocale,
                translations,
                setLocale,
                _,
                t: _, // Common alias
                getAvailableLocales,
                isLoading,
            }}
        >
            {props.children}
        </I18nContext.Provider>
    );
}
