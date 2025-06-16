import { createContext, useContext, createSignal } from 'solid-js';
import { NavLink } from '@lib/nav';
import type { ParentComponent } from 'solid-js';

type NavContextType = {
    navLinks: () => NavLink[];
    setNavLinks: (links: NavLink[]) => void;
};

const NavContext = createContext<NavContextType>();

export const NavProvider: ParentComponent = (props) => {
    const [navLinks, setNavLinks] = createSignal<NavLink[]>([]);
    const context: NavContextType = { navLinks, setNavLinks };

    return (
        <NavContext.Provider value={context}>
            {props.children}
        </NavContext.Provider>
    );
};

export function useNav() {
    const context = useContext(NavContext);
    if (!context) throw new Error('useNav must be used within NavProvider');
    return context;
}
