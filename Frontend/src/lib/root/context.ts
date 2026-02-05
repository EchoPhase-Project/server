import { createContext, useContext } from 'solid-js';

export interface RootMetaContextValue {
    setTitle: (title: string) => void;
    setDescription: (description: string) => void;
}

export const RootMetaContext = createContext<RootMetaContextValue>();

export function useRootMeta() {
    const ctx = useContext(RootMetaContext);
    if (!ctx) {
        throw new Error('useRootMeta must be used inside <Root>');
    }
    return ctx;
}
