import { createRenderEffect } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { useNav } from '@lib/nav';
import { useRootMeta } from '@lib/root';
import { useAuth } from '@lib/api';
import { AiOutlineInfoCircle } from 'solid-icons/ai';
import { TbBuildingEstate } from 'solid-icons/tb';
import { useI18n } from '@lib/i18n';

export default function Home() {
    const { setTitle, setDescription } = useRootMeta();
    const { setNavLinks } = useNav();
    const { logout } = useAuth();
    const { t } = useI18n();

    const navigate = useNavigate();

    createRenderEffect(() => {
        setTitle('Home · EchoPhase');
        setDescription(
            'Secure API bridge and real-time communication platform for modern distributed applications.'
        );
        setNavLinks([
            {
                icon: TbBuildingEstate,
                text: t('nav.home', 'Home'),
                events: {
                    onClick: () => navigate('/'),
                },
            },
            {
                icon: AiOutlineInfoCircle,
                text: t('nav.about', 'About'),
                events: {
                    onClick: () => navigate('/about'),
                },
            },
            {
                icon: AiOutlineInfoCircle,
                text: t('nav.logout', 'Logout'),
                events: {
                    onClick: () => logout(),
                },
            },
        ]);
    });

    return (
        <div>
            <h1>Home page</h1>
        </div>
    );
}
