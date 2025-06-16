import { render } from 'solid-js/web';
import Layout from './Layout';
import RouterComponent from './Router';
import i18n from '@lib/i18n/i18n';

async function bootstrap() {
    await i18n.loadTranslations(i18n.getInitialLocale());
    render(
        () => <RouterComponent root={Layout} />,
        document.getElementById('root') as HTMLElement
    );
}

bootstrap();
