import { LanguageDropdown } from './LanguageDropdown';
import { ThemeToggle } from './ThemeToggle';

export function Header() {
    return (
        <header class="header">
            <ThemeToggle />
            <LanguageDropdown />
        </header>
    );
}
