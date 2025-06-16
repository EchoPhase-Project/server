import { createSignal } from 'solid-js';
import { useNavigate } from '@solidjs/router';
import { FaRegularUser } from 'solid-icons/fa';
import { BiRegularLock } from 'solid-icons/bi';
import { AiOutlineTags } from 'solid-icons/ai';
import { i18n } from '@lib/i18n';
import '@styles/forms.scss';

interface RegisterFormData {
    name: string;
    username: string;
    password: string;
}

const [registerForm, setRegisterForm] = createSignal<RegisterFormData>({
    name: '',
    username: '',
    password: '',
});

type ErrorItem = { code: string; description: string };
const [errors, setErrors] = createSignal<Record<string, ErrorItem[]>>({});

async function handleRegister(e: Event) {
    e.preventDefault();
    const res = await fetch('/api/v1/auth/register', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(registerForm()),
    });

    if (res.ok) {
        setErrors({});
        useNavigate('/');
    } else {
        const result: ErrorItem[] = await res.json();
        setErrors({ register: result });
    }
}

export default function RegisterForm() {
    return (
        <form class="vert-form" onSubmit={handleRegister}>
            <h4 class="form-title">{i18n._('Sign Up')}</h4>
            <div class="form-group">
                <FaRegularUser class="input-icon" />
                <input
                    class="form-style"
                    type="text"
                    autocomplete="off"
                    placeholder={i18n._('Your Full Name')}
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
                    placeholder={i18n._('Your Username')}
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
                    placeholder={i18n._('Your Password')}
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
                {i18n._('SUBMIT')}
            </button>
            {errors().register && (
                <ul class="errorlist">
                    {errors().register.map((error) => (
                        <li>{error.description}</li>
                    ))}
                </ul>
            )}
        </form>
    );
}
