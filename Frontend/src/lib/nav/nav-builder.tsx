import { For } from 'solid-js';
import { NavLink } from '@lib/nav';
import { useI18n } from '@lib/i18n';

export function NavBuilder(props: { links: NavLink[] }) {
    const { t } = useI18n();

    return (
        <nav class="navbar">
            <ul class="navbar-nav">
                <For each={props.links}>
                    {(link, index) => {
                        const Icon = link.icon;
                        return (
                            <li class={index() === 0 ? 'logo' : 'nav-item'}>
                                <a href={link.routeName} class="nav-link">
                                    <i class="nav-icon">
                                        <Icon />
                                    </i>
                                    <span class="link-text">
                                        {t(link.i18n, link.text)}
                                    </span>
                                </a>
                            </li>
                        );
                    }}
                </For>
            </ul>
        </nav>
    );
}
