import { createSignal, onMount } from 'solid-js';
import { Csrf } from '@lib/csrf';

export default function Register() {
    const [name, setName] = createSignal('');
    const [username, setUsername] = createSignal('');
    const [password, setPassword] = createSignal('');
    const [csrfToken, setCsrfToken] = createSignal<string | null>(null);

    onMount(async () => {
        const token = await Csrf.getToken();
        setCsrfToken(token);
    });

    async function handleRegister() {
        const res = await fetch('/api/v1/auth/register', {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json',
                ...(csrfToken() ? { 'X-CSRF-TOKEN': csrfToken() } : {}),
            },
            body: JSON.stringify({
                name: name(),
                username: username(),
                password: password(),
            }),
        });
        alert(res.ok ? 'Registered!' : 'Failed');
    }

    return (
        <div>
            <h2>Register</h2>
            <input
                placeholder="Name"
                onInput={(e) => setName(e.currentTarget.value)}
            />
            <input
                placeholder="Username"
                onInput={(e) => setUsername(e.currentTarget.value)}
            />
            <input
                type="password"
                placeholder="Password"
                onInput={(e) => setPassword(e.currentTarget.value)}
            />
            <button onClick={handleRegister}>Register</button>
        </div>
    );
}
