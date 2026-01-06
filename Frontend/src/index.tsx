import { render } from 'solid-js/web';
import Layout from './Layout';
import RouterComponent from './Router';

async function bootstrap() {
    render(
        () => <RouterComponent root={Layout} />,
        document.getElementById('root') as HTMLElement
    );
}

bootstrap();
