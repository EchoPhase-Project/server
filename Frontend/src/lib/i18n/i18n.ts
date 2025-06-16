import { createSignal } from 'solid-js';

export type Translations = Record<string, string>;

interface LocaleDefinition {
    nativeName: string;
    translations?: Translations;
}

const [currentLocale, setCurrentLocale] = createSignal<string>('en');
const [translations, setTranslations] = createSignal<Translations>({});
const defaultLocale = 'en';

export class I18n {
    private locales: Record<string, LocaleDefinition> = {
        en: { nativeName: 'English' },
        ro: { nativeName: 'Română' },
        ru: { nativeName: 'Русский' },
    };

    private STORAGE_KEY = 'app_locale';

    private getBrowserLocale(): string | null {
        if (typeof navigator !== 'undefined') {
            const lang =
                navigator.language ||
                (navigator as Navigator & { userLanguage?: string })
                    .userLanguage;
            return lang?.slice(0, 2).toLowerCase() ?? null;
        }
        return null;
    }

    private getInitialLocale(): string {
        if (typeof localStorage !== 'undefined') {
            const stored = localStorage.getItem(this.STORAGE_KEY);
            if (stored && this.locales[stored]) {
                return stored;
            }
        }
        const browserLocale = this.getBrowserLocale();
        if (browserLocale && this.locales[browserLocale]) {
            return browserLocale;
        }
        return defaultLocale;
    }

    public async loadTranslations(locale: string): Promise<void> {
        if (!this.locales[locale]) {
            console.warn(`Locale "${locale}" not supported, fallback to 'en'`);
            locale = defaultLocale;
        }

        try {
            const loaded: Translations = (
                await import(`@lib/i18n/locales/${locale}.ts`)
            ).default;

            this.locales[locale].translations = loaded;
            this.setTranslations(loaded);
            this.setCurrentLocale(locale);

            if (typeof localStorage !== 'undefined') {
                localStorage.setItem(this.STORAGE_KEY, locale);
            }
        } catch (e) {
            console.error('Error loading locale', locale, e);
        }
    }

    public setLocale(locale: string): void {
        if (this.locales[locale] && locale !== this.currentLocale()) {
            this.loadTranslations(locale);
        }
    }

    public _(key: string): string {
        return this.translations()[key] ?? key;
    }

    public getAvailableLocales(): { code: string; nativeName: string }[] {
        return Object.entries(this.locales).map(([code, def]) => ({
            code,
            nativeName: def.nativeName,
        }));
    }

    public currentLocale = currentLocale;
    public translations = translations;

    private setCurrentLocale = setCurrentLocale;
    private setTranslations = setTranslations;
}

export const i18n = new I18n();

export default i18n;
