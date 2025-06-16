import { Component, Show } from 'solid-js';
import type { ParentProps } from 'solid-js';
import { useNav, NavBuilder } from '@lib/nav';
import { Header } from '@/components/Header';
import '@styles/global.scss';
import '@styles/navbar.scss';

const Layout: Component<ParentProps> = (props) => {
    const { navLinks } = useNav();

    return (
        <div class="container">
            <Show when={navLinks().length > 0}>
                <NavBuilder links={navLinks()} />
            </Show>
            <Header />
            <main>{props.children}</main>
        </div>
    );
};

export default Layout;
