import { Component, lazy } from 'solid-js';
import { Router } from '@solidjs/router';
import { NavProvider } from '@lib/nav';
import { I18nProvider } from '@lib/i18n';
import { AuthProvider } from '@lib/api';
import { ThemeProvider } from '@lib/theme';
import { CryptoProvider } from '@lib/crypto25519';
import { CursorProvider } from '@lib/cursor';

interface RouterComponentProps {
    base?: string;
    root: Component;
}

const routes = [
    {
        path: '/',
        component: lazy(() => import('./components/Home')),
    },
    {
        path: '/about',
        component: lazy(() => import('./components/About')),
    },
    {
        path: '/auth',
        component: lazy(() => import('./components/Auth')),
    },
    {
        path: '/cursor',
        component: lazy(() => import('./components/CursorExample')),
    },
    {
        path: '/drag',
        component: lazy(() => import('./components/DragExample')),
    },
    {
        path: '/spin',
        component: lazy(() => import('./components/SpinTest')),
    },
    {
        path: '/table',
        component: lazy(() => import('./components/TableTest')),
    },
    {
        path: '*404',
        component: lazy(() => import('./components/NotFound')),
    },
];

const RouterComponent = (props: RouterComponentProps) => (
    <CryptoProvider>
        <ThemeProvider>
            <I18nProvider>
                <AuthProvider>
                    <CursorProvider>
                        <NavProvider>
                            <Router base={props.base} root={props.root}>
                                {routes}
                            </Router>
                        </NavProvider>
                    </CursorProvider>
                </AuthProvider>
            </I18nProvider>
        </ThemeProvider>
    </CryptoProvider>
);

export default RouterComponent;
