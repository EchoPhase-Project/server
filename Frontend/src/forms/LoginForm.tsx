import { createSignal } from 'solid-js';
import { FaRegularUser } from 'solid-icons/fa';
import { BiRegularLock } from 'solid-icons/bi';
import { useNavigate } from '@solidjs/router';
import { useAuth } from '@lib/api';
import type { LoginCredentials } from '@lib/api';
import { useI18n } from '@lib/i18n';
import '@styles/forms.scss';

type ErrorItem = { code: string; description: string };

export default function LoginForm() {
    const i18n = useI18n();
    const auth = useAuth();
    const navigate = useNavigate();

    const [loginForm, setLoginForm] = createSignal<LoginCredentials>({
        username: '',
        password: '',
    });

    const [errors, setErrors] = createSignal<Record<string, ErrorItem[]>>({});

    const isDisabled = () => auth.isLoading() || i18n.isLoading();

    async function handleLogin(e: Event) {
        e.preventDefault();

        const result = await auth.login(loginForm());

        if (result.success) {
            setErrors([]);
            navigate('/');
        } else {
            setErrors(result.error || []);
        }
    }

    return (
        <form class="vert-form" onSubmit={handleLogin}>
            <h4 class="form-title">{i18n.t('auth.login', 'Log In')}</h4>

            <div class="form-group">
                <FaRegularUser class="input-icon" />
                <input
                    class="form-style"
                    type="text"
                    autocomplete="off"
                    placeholder={i18n.t('form.username', 'Your Username')}
                    value={loginForm().username}
                    disabled={isDisabled()}
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
                    placeholder={i18n.t('form.password', 'Your Password')}
                    value={loginForm().password}
                    disabled={isDisabled()}
                    onInput={(e) =>
                        setLoginForm((f) => ({
                            ...f,
                            password: e.currentTarget.value,
                        }))
                    }
                />
            </div>

            <button
                type="submit"
                class="form-button button"
                disabled={isDisabled()}
                aria-busy={isDisabled()}
            >
                {isDisabled()
                    ? i18n.t('loading', 'Loading...')
                    : i18n.t('form.submit', 'SUBMIT')}
            </button>

            {errors().login && (
                <ul class="errorlist">
                    {errors().login.map((error) => (
                        <li>{i18n.t(error.code, error.description)}</li>
                    ))}
                </ul>
            )}
        </form>
    );
}
