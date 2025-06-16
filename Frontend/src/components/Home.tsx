import Head from './Head';
import { onMount } from 'solid-js';
import { useNav } from '@lib/nav';
import { AiOutlineInfoCircle } from 'solid-icons/ai';
import { TbBuildingEstate } from 'solid-icons/tb';

export default function Home() {
    const { setNavLinks } = useNav();

    onMount(() => {
        setNavLinks([
            { routeName: '/', icon: TbBuildingEstate, text: 'Home' },
            { routeName: '/about', icon: AiOutlineInfoCircle, text: 'About' },
        ]);
    });

    return (
        <div>
            <Head
                title="Home Page - SolidJS"
                description="Welcome to the SolidJS Home Page"
            />
            <h1>Home page</h1>
        </div>
    );
}
