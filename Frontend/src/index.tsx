import { render } from 'solid-js/web';
import Root from './Root';
import Layout from './Layout';
import RouterComponent from './Router';

render(
    () => (
        <Root>
            <RouterComponent root={Layout} />
        </Root>
    ),
    document.getElementById('root') as HTMLElement
);
