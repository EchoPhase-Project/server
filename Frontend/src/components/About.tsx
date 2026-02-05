import { onMount } from 'solid-js';
import { useRootMeta } from '@lib/root';
import { IoBeerOutline } from 'solid-icons/io';

const About = () => {
    const { setTitle, setDescription } = useRootMeta();

    onMount(() => {
        setTitle('About · EchoPhase');
        setDescription('Learn more about EchoPhase and its architecture');
    });

    return (
        <div>
            <h1>About Page</h1>
            <IoBeerOutline style={{ 'font-size': '48px', color: 'orange' }} />
        </div>
    );
};

export default About;
