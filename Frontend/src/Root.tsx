import { ParentComponent, createSignal } from 'solid-js';
import { MetaProvider, Title, Meta, Link } from '@solidjs/meta';
import { RootMetaContext } from '@lib/root';
import appleTouchIcon from '@assets/icons/apple-touch-icon.png';
import legacyIcon from '@assets/icons/favicon.ico';
import mediumIcon from '@assets/icons/favicon-32x32.png';
import smallIcon from '@assets/icons/favicon-16x16.png';

const Root: ParentComponent = (props) => {
    const [title, setTitle] = createSignal('EchoPhase');
    const [description, setDescription] = createSignal('API Bridge');

    return (
        <MetaProvider>
            <RootMetaContext.Provider value={{ setTitle, setDescription }}>
                <>
                    {/* ---------- HEAD ---------- */}
                    <Title>{title()}</Title>

                    <Meta
                        name="viewport"
                        content="width=device-width, initial-scale=1.0"
                    />
                    <Meta name="author" content="Dustyn" />

                    <Meta name="description" content={description()} />

                    <Meta property="og:type" content="website" />
                    <Meta property="og:url" content="https://echophase.app" />
                    <Meta property="og:description" content={description()} />

                    <Meta name="twitter:card" content="summary_large_image" />
                    <Meta name="twitter:title" content={title()} />
                    <Meta name="twitter:description" content={description()} />

                    <Link
                        rel="icon"
                        type="image/vnd.microsoft.icon"
                        href={legacyIcon}
                    />
                    <Link
                        rel="apple-touch-icon"
                        sizes="180x180"
                        href={appleTouchIcon}
                    />
                    <Link
                        rel="icon"
                        sizes="32x32"
                        type="image/png"
                        href={mediumIcon}
                    />
                    <Link
                        rel="icon"
                        sizes="16x16"
                        type="image/png"
                        href={smallIcon}
                    />

                    {/* ---------- BODY ---------- */}
                    {props.children}
                </>
            </RootMetaContext.Provider>
        </MetaProvider>
    );
};

export default Root;
