import { Component, lazy } from 'solid-js';
import { Router } from '@solidjs/router';
import { NavProvider } from '@lib/nav';

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
        path: '/drag',
        component: lazy(() => import('./components/DragExample')),
    },
    {
        path: '/spin',
        component: lazy(() => import('./components/SpinTest')),
    },
    {
        path: '*404',
        component: lazy(() => import('./components/NotFound')),
    },
];

const RouterComponent = (props: RouterComponentProps) => (
    <NavProvider>
        <Router base={props.base} root={props.root}>
            {routes}
        </Router>
    </NavProvider>
);

export default RouterComponent;
