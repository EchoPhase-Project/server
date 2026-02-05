import { Table, Cell, Row } from './Table';
import { Component, Show } from 'solid-js';

type Webhook = {
    id: string;
    userId: string;
    user: {
        userName: string;
    };
    url: string;
    status: 'active' | 'inactive' | 'pending';
    intents: string;
    createdAt: Date;
};

const NoWebhooksPlaceholder: Component = () => {
    return <>No webhooks yet</>;
};

const formatDate = (date: Date): string => {
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

    if (diffDays === 0) return 'Today';
    if (diffDays === 1) return 'Yesterday';
    if (diffDays < 7) return `${diffDays} days ago`;
    if (diffDays < 30) return `${Math.floor(diffDays / 7)} weeks ago`;
    return `${Math.floor(diffDays / 30)} months ago`;
};

export default function WebhooksPage() {
    const webhooks: Webhook[] = [
        {
            id: '1',
            userId: 'user-123',
            user: { userName: 'john_doe' },
            url: 'https://example.com/webhook',
            status: 'active',
            intents: 'messages, reactions',
            createdAt: new Date('2024-01-15'),
        },
        {
            id: '2',
            userId: 'user-456',
            user: { userName: 'jane_smith' },
            url: 'https://api.example.com/hook',
            status: 'inactive',
            intents: 'all',
            createdAt: new Date('2024-02-01'),
        },
        {
            id: '3',
            userId: 'user-789',
            user: { userName: 'bob_wilson' },
            url: 'https://hooks.example.com/notify',
            status: 'pending',
            intents: 'messages',
            createdAt: new Date('2024-02-03'),
        },
    ];

    const headers = ['Username', 'URL', 'Status', 'Intents', 'Created'];

    return (
        <div class="webhooks-container">
            <h1>Webhooks Management</h1>

            <Table
                data={webhooks}
                headers={headers}
                placeholder={NoWebhooksPlaceholder}
                tableProps={{
                    class: 'webhooks',
                }}
                render={({ item: webhook }) => (
                    <Row>
                        <Cell
                            label="Username"
                            text={webhook.user.userName}
                            props={{ class: 'username' }}
                        />
                        <Cell
                            label="URL"
                            text={webhook.url}
                            props={{ class: 'url' }}
                        />
                        <Cell
                            label="Status"
                            text={webhook.status}
                            props={{ class: 'status' }}
                        />
                        <Cell
                            label="Intents"
                            text={webhook.intents}
                            props={{ class: 'intents' }}
                        />
                        <Cell
                            label="Created"
                            text={formatDate(webhook.createdAt)}
                            props={{ class: 'date' }}
                            events={{
                                onClick: () =>
                                    console.log(
                                        'Clicked date for webhook:',
                                        webhook.id
                                    ),
                            }}
                        />
                    </Row>
                )}
            />
        </div>
    );
}
