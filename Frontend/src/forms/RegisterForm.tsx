import { createSignal } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { FaRegularUser } from 'solid-icons/fa';
import { BiRegularLock } from 'solid-icons/bi';
import { AiOutlineTags } from 'solid-icons/ai';
import { useAuth } from '@lib/api';
import type { RegisterData } from '@lib/api';
import { useI18n } from '@lib/i18n';
import '@styles/forms.scss';

type ErrorItem = { code: string; description: string };

export default function RegisterForm() {
    const [registerForm, setRegisterForm] = createSignal<RegisterData>({
        name: '',
        username: '',
        password: '',
    });

    const [errors, setErrors] = createSignal<Record<string, ErrorItem[]>>({});
    const auth = useAuth();
    const navigate = useNavigate();

    const i18n = useI18n();
    const isDisabled = () => i18n.isLoading();

    async function handleRegister(e: Event) {
        e.preventDefault();

        const result = await auth.register(registerForm());

        if (result.success) {
            setErrors([]);
            navigate('/');
        } else {
            setErrors(result.error || []);
        }
    }

    return (
        <form class="vert-form" onSubmit={handleRegister}>
            <h4 class="form-title">{i18n.t('auth.signup', 'Sign Up')}</h4>
            <div class="form-group">
                <FaRegularUser class="input-icon" />
                <input
                    class="form-style"
                    type="text"
                    autocomplete="off"
                    disabled={isDisabled()}
                    placeholder={i18n.t('form.fullname', 'Your Full Name')}
                    value={registerForm().name}
                    onInput={(e) =>
                        setRegisterForm((f) => ({
                            ...f,
                            name: e.currentTarget.value,
                        }))
                    }
                />
            </div>
            <div class="form-group">
                <AiOutlineTags class="input-icon" />
                <input
                    class="form-style"
                    type="text"
                    autocomplete="off"
                    disabled={isDisabled()}
                    placeholder={i18n.t('form.username', 'Your Username')}
                    value={registerForm().username}
                    onInput={(e) =>
                        setRegisterForm((f) => ({
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
                    disabled={isDisabled()}
                    placeholder={i18n.t('form.password', 'Your Password')}
                    value={registerForm().password}
                    onInput={(e) =>
                        setRegisterForm((f) => ({
                            ...f,
                            password: e.currentTarget.value,
                        }))
                    }
                />
            </div>
            <button type="submit" class="form-button button">
                {isDisabled()
                    ? i18n.t('loading', 'Loading...')
                    : i18n.t('form.submit', 'SUBMIT')}
            </button>
            {errors().register && (
                <ul class="errorlist">
                    {errors().register.map((error) => (
                        <li>{i18n.t(error.code, error.description)}</li>
                    ))}
                </ul>
            )}
        </form>
    );
}
