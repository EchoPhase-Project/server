import { Component, JSX } from 'solid-js';

export interface NavLink {
    icon: Component;
    text: string;
    events?: Partial<
        Record<
            keyof JSX.EventHandlerUnion<HTMLButtonElement, Event>,
            JSX.EventHandlerUnion<HTMLButtonElement, Event>
        >
    >;
}
