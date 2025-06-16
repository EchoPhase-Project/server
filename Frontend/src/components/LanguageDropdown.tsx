import { createSignal, onCleanup, For, Show } from 'solid-js';
import { i18n } from '@lib/i18n';
import '@styles/dropdown.scss';

export function LanguageDropdown(props: { returnUrl?: string }) {
    const [isOpen, setIsOpen] = createSignal(false);
    let dropdownRef: HTMLDivElement | undefined;

    const handleClickOutside = (event: MouseEvent) => {
        if (dropdownRef && !dropdownRef.contains(event.target as Node)) {
            setIsOpen(false);
            document.removeEventListener('click', handleClickOutside);
        }
    };

    const toggleDropdown = () => {
        const newState = !isOpen();
        setIsOpen(newState);

        if (newState) {
            document.addEventListener('click', handleClickOutside);
        } else {
            document.removeEventListener('click', handleClickOutside);
        }
    };

    onCleanup(() => {
        document.removeEventListener('click', handleClickOutside);
    });

    const locales = i18n.getAvailableLocales();

    return (
        <div class="dropdown" ref={dropdownRef} data-open={isOpen()}>
            <div class="select" onClick={toggleDropdown}>
                <span class="noselect">
                    {locales
                        .find((l) => l.code === i18n.currentLocale())
                        ?.code.toUpperCase() ?? i18n._('Select Language')}
                </span>
                <i class="uil uil-arrow-down"></i>
            </div>

            <div class="dropdown-menu">
                <For each={locales}>
                    {(locale) => (
                        <Show when={locale.code !== i18n.currentLocale()}>
                            <div
                                class="dropdown-item"
                                onClick={(e) => {
                                    e.preventDefault();
                                    i18n.setLocale(locale.code);
                                    if (props.returnUrl) {
                                        window.location.href = props.returnUrl;
                                    }
                                }}
                            >
                                <span class="noselect">
                                    {locale.code.toUpperCase()}
                                </span>
                            </div>
                        </Show>
                    )}
                </For>
            </div>
        </div>
    );
}
