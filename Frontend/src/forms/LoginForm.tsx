import { createSignal } from 'solid-js';
import { FaRegularUser } from 'solid-icons/fa';
import { BiRegularLock } from 'solid-icons/bi';
import { useNavigate } from '@solidjs/router';
import { CsrfService } from '@lib/csrf';
import { i18n } from '@lib/i18n';
import '@styles/forms.scss';

interface LoginFormData {
    username: string;
    password: string;
}

const [loginForm, setLoginForm] = createSignal<LoginFormData>({
    username: '',
    password: '',
});

type ErrorItem = { code: string; description: string };
const [errors, setErrors] = createSignal<Record<string, ErrorItem[]>>({});

async function handleLogin(e: Event) {
    e.preventDefault();
    const res = await fetch('/api/v1/auth/login', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(loginForm()),
    });

    if (res.ok) {
        const newToken = res.headers.get('X-CSRF-TOKEN');
        if (newToken) CsrfService.saveToken(newToken);
        setErrors({});
        useNavigate('/');
    } else {
        setErrors({ login: ['Unauthorized'] });
    }
}

export default function LoginForm() {
    return (
        <form class="vert-form" onSubmit={handleLogin}>
            <h4 class="form-title">{i18n._('Log In')}</h4>
            <div class="form-group">
                <FaRegularUser class="input-icon" />
                <input
                    class="form-style"
                    type="text"
                    autocomplete="off"
                    placeholder={i18n._('Your Username')}
                    value={loginForm().username}
                    onInput={(e) =>
                        setLoginForm((f) => ({
                            ...f,
                            username: e.currentTarget.value,
                        }))
                    }
                />
            </div>
            <div class="form-group">
                <BiRegularLock class="input-icon" />
                <input
                    class="form-style"
                    type="password"
                    autocomplete="off"
                    placeholder={i18n._('Your Password')}
                    value={loginForm().password}
                    onInput={(e) =>
                        setLoginForm((f) => ({
                            ...f,
                            password: e.currentTarget.value,
                        }))
                    }
                />
            </div>
            <button type="submit" class="form-button button">
                {i18n._('SUBMIT')}
            </button>
            {errors().login && (
                <ul class="errorlist">
                    {errors().login.map((error) => (
                        <li>{error}</li>
                    ))}
                </ul>
            )}
        </form>
    );
}
