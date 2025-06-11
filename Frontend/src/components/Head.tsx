import { Title, Meta, MetaProvider } from '@solidjs/meta';

interface HeadProps {
    title: string;
    description: string;
}

const Head = (props: HeadProps) => {
    return (
        <MetaProvider>
            <Title>{props.title}</Title>
            <Meta name="description" content={props.description} />
        </MetaProvider>
    );
};

export default Head;
