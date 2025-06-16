import { themeManager } from '@lib/theme';
import { FaRegularMoon } from 'solid-icons/fa';
import { OcSun2 } from 'solid-icons/oc';
import '@styles/theme.scss';

export function ThemeToggle() {
    const [theme] = [themeManager.theme];
    return (
        <div class="theme-toggle" onClick={() => themeManager.toggle()}>
            {theme() === 'dark' ? (
                <FaRegularMoon class="theme" />
            ) : (
                <OcSun2 class="theme" />
            )}
        </div>
    );
}
