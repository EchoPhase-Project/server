import { For } from 'solid-js';
import { NavLink } from '@lib/nav';
import '@styles/navbar.scss';

export function NavBuilder(props: { links: NavLink[] }) {
    return (
        <nav class="navbar">
            <ul class="navbar-nav">
                <For each={props.links}>
                    {(link, index) => {
                        return (
                            <li class={index() === 0 ? 'logo' : 'nav-item'}>
                                <div
                                    class="nav-link"
                                    role="button"
                                    tabIndex={0}
                                    aria-label={link.text}
                                    {...link.events}
                                >
                                    <i class="nav-icon">
                                        <link.icon />
                                    </i>
                                    <span class="link-text">{link.text}</span>
                                </div>
                            </li>
                        );
                    }}
                </For>
            </ul>
        </nav>
    );
}
