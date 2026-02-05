import {
    createContext,
    useContext,
    createSignal,
    createResource,
    onMount,
    JSX,
} from 'solid-js';
import localesList from '@static/locales.json';

/**
 * Translations type supporting nested structures
 * Values can be strings or nested translation objects
 */
export type Translations = Record<string, string | Translations>;

interface LocaleDefinition {
    nativeName: string;
}

interface I18nContextType {
    currentLocale: () => string;
    translations: () => Translations | undefined;
    setLocale: (locale: string) => Promise<void>;
    _: (key: string, fallback?: string) => string;
    t: (key: string, fallback?: string) => string;
    availableLocales: () => { code: string; nativeName: string }[] | undefined;
    isLoading: () => boolean;
}

const defaultLocale = 'en';
const STORAGE_KEY = 'app_locale';

/**
 * Pre-load all locale loaders using import.meta.glob
 * Vite will analyze this at build time and create separate chunks
 */
const localeLoaders = import.meta.glob<{ default: string }>(
    '@/static/locales/*.json',
    {
        query: '?raw',
        import: 'default',
        eager: false,
    }
);

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
    // Locale definitions from static config
    const localeDefinitions: Record<string, LocaleDefinition> = localesList;

    /**
     * Gets list of available locales from glob pattern
     * Combines file system data with locale definitions
     * @returns Array of locale objects with code and native name
     */
    function getAvailableLocales(): { code: string; nativeName: string }[] {
        return Object.keys(localeLoaders)
            .map((path) => {
                // Extract locale code from path: /src/static/locales/en.json -> en
                const match = path.match(/\/([^/]+)\.json$/);
                const code = match?.[1];

                if (!code || !localeDefinitions[code]) return null;

                return {
                    code,
                    nativeName: localeDefinitions[code].nativeName,
                };
            })
            .filter(
                (locale): locale is { code: string; nativeName: string } =>
                    locale !== null
            );
    }

    /**
     * Resource for available locales
     * Loads once and caches the result
     */
    const [availableLocales] = createResource(getAvailableLocales);

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
            if (stored && localeDefinitions[stored]) {
                return stored;
            }
        }

        // 2. Check browser locale
        const browserLocale = getBrowserLocale();
        if (browserLocale && localeDefinitions[browserLocale]) {
            return browserLocale;
        }

        // 3. Fallback to default
        return defaultLocale;
    }

    // Current locale signal initialized with browser locale
    const [currentLocale, setCurrentLocale] =
        createSignal<string>(getInitialLocale());

    // Translation cache to avoid reloading
    const translationCache = new Map<string, Translations>();

    /**
     * Normalizes locale loaders to use locale code as key
     * Makes it easier to access loaders by locale code
     */
    const normalizedLoaders = Object.keys(localeLoaders).reduce(
        (acc, path) => {
            const match = path.match(/\/([^/]+)\.json$/);
            const code = match?.[1];
            if (code) {
                acc[code] = localeLoaders[path];
            }
            return acc;
        },
        {} as Record<string, () => Promise<string>>
    );

    /**
     * Resource for translations based on current locale
     * Automatically refetches when locale changes
     */
    const [translations, { refetch }] = createResource(
        currentLocale,
        async (locale): Promise<Translations> => {
            // Check cache first
            if (translationCache.has(locale)) {
                return translationCache.get(locale)!;
            }

            try {
                // Get the loader for this locale using normalized map
                const loader = normalizedLoaders[locale];

                if (!loader) {
                    throw new Error(`Loader not found for locale: ${locale}`);
                }

                // Load and parse translations
                const raw = await loader();
                const loaded: Translations = JSON.parse(raw);

                // Cache for future use
                translationCache.set(locale, loaded);

                // Update HTML lang attribute for accessibility
                if (typeof document !== 'undefined') {
                    document.documentElement.lang = locale;
                }

                return loaded;
            } catch (e) {
                console.error(`Error loading locale "${locale}":`, e);

                // Fallback to default locale if not already using it
                if (locale !== defaultLocale) {
                    console.warn(
                        `Attempting to load fallback locale: ${defaultLocale}`
                    );

                    // Check cache for default locale
                    if (translationCache.has(defaultLocale)) {
                        return translationCache.get(defaultLocale)!;
                    }

                    // Load default locale
                    const defaultLoader = normalizedLoaders[defaultLocale];

                    if (defaultLoader) {
                        const raw = await defaultLoader();
                        const loaded = JSON.parse(raw);
                        translationCache.set(defaultLocale, loaded);
                        return loaded;
                    }
                }

                // Ultimate fallback: empty translations
                return {};
            }
        }
    );

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
        const available = availableLocales();
        const availableCodes = available?.map((l) => l.code) || [];

        // 1. Check localStorage
        if (typeof localStorage !== 'undefined') {
            const stored = localStorage.getItem(STORAGE_KEY);
            if (stored && availableCodes.includes(stored)) {
                return stored;
            }
        }

        // 2. Check browser locale
        const browserLocale = getBrowserLocale();
        if (browserLocale && availableCodes.includes(browserLocale)) {
            return browserLocale;
        }

        // 3. Fallback to default
        return defaultLocale;
    }

    /**
     * Changes current locale and loads its translations
     * @param locale - Target locale code
     */
    async function setLocale(locale: string): Promise<void> {
        const available = availableLocales();
        const isValid = available?.some((l) => l.code === locale);

        if (!isValid) {
            console.warn(
                `Locale "${locale}" not supported, fallback to '${defaultLocale}'`
            );
            locale = defaultLocale;
        }

        if (locale !== currentLocale()) {
            setCurrentLocale(locale);

            // Persist to localStorage
            if (typeof localStorage !== 'undefined') {
                localStorage.setItem(STORAGE_KEY, locale);
            }
        }
    }

    /**
     * Translates a key to current locale
     * Supports nested keys with dot notation
     * @param key - Translation key (e.g., 'greeting.hello')
     * @param fallback - Optional fallback text (defaults to key)
     * @returns Translated string or fallback
     *
     * @example
     * _('greeting.hello') // Returns translation
     * _('user.profile.name', 'Name') // Returns nested translation
     * _('missing.key', 'Default text') // Returns 'Default text'
     */
    function _(key: string, fallback?: string): string {
        const trans = translations();

        if (!trans) {
            return fallback ?? key;
        }

        const parts = key.split('.');
        let current: string | Translations | undefined = trans;

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
     * Returns loading state of translations
     * Combines resource loading state with availability check
     */
    function isLoading(): boolean {
        return translations.loading || availableLocales.loading;
    }

    // Initialize locale on mount
    onMount(() => {
        // Locale is already initialized in signal with getInitialLocale()
        // Resource will automatically load translations for currentLocale()
    });

    return (
        <I18nContext.Provider
            value={{
                currentLocale,
                translations,
                setLocale,
                _,
                t: _,
                availableLocales,
                isLoading,
            }}
        >
            {props.children}
        </I18nContext.Provider>
    );
}
