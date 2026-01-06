import { createContext, useContext, ParentComponent } from 'solid-js';
import { Crypto25519Service } from '@lib/crypto25519';

export interface CryptoContextType {
    service: Crypto25519Service;
}

const CryptoContext = createContext<CryptoContextType>();

export const CryptoProvider: ParentComponent = (props) => {
    const service = new Crypto25519Service();

    return (
        <CryptoContext.Provider value={{ service }}>
            {props.children}
        </CryptoContext.Provider>
    );
};

export function useCrypto(): Crypto25519Service {
    const ctx = useContext(CryptoContext);
    if (!ctx) throw new Error('useCrypto must be used within CryptoProvider');
    return ctx.service;
}
