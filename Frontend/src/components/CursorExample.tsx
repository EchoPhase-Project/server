import { onMount } from 'solid-js';
import { useCursor } from '@lib/cursor';
import '@styles/cursor.css';

export default function CursorComponent() {
    const cursor = useCursor();

    onMount(() => {
        cursor.interact('.card', {
            cursor: {
                grab: true,
                sizeRate: 1.2,
                classList: ['cursor-hover'],
            },
            target: {
                movementRate: 0.3,
                classList: ['card-active'],
            },
            centrify: true,
        });
    });

    return <div class="card">Interactive Card</div>;
}
