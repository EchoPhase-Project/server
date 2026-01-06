import { useTheme } from '@lib/theme';
import { FaRegularMoon } from 'solid-icons/fa';
import { OcSun2 } from 'solid-icons/oc';
import '@styles/theme.scss';

export function ThemeToggle() {
    const [theme, setTheme] = useTheme();

    const toggle = () => {
        setTheme(theme() === 'light' ? 'dark' : 'light');
    };

    return (
        <div class="theme-toggle" onClick={toggle}>
            {theme() === 'dark' ? (
                <FaRegularMoon class="theme" />
            ) : (
                <OcSun2 class="theme" />
            )}
        </div>
    );
}
