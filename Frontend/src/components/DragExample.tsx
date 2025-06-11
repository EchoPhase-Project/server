import { DragAndDrop } from '@lib/drag-and-drop';
import { IoAlert } from 'solid-icons/io';
import { onMount } from 'solid-js';

export default function DragDropExample() {
    let dragRef: HTMLElement | undefined;

    onMount(() => {
        if (!dragRef) return;

        DragAndDrop.bind({
            currentTarget: dragRef,
            target: dragRef,
            currentTarget_in_f: () => console.log('Drag started'),
            currentTarget_f: () => console.log('Dragging...'),
            currentTarget_out_f: () => console.log('Drag ended'),
        });
    });

    return (
        <div
            ref={(el) => (dragRef = el)}
            style={{
                width: '150px',
                'user-select': 'none',
                'background-color': 'skyblue',
                'touch-action': 'none',
                height: '150px',
                cursor: 'grab',
                position: 'relative',
            }}
        >
            <IoAlert />
            Перетащи меня
        </div>
    );
}
