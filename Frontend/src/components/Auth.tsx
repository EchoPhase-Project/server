import { createSignal, onMount } from 'solid-js';
import { useI18n } from '@lib/i18n';
import RegisterForm from '@forms/RegisterForm';
import LoginForm from '@forms/LoginForm';
import { BsArrowUpRight } from 'solid-icons/bs';
import { useNav } from '@lib/nav';
import '@styles/auth.scss';

export default function Auth() {
    const [isRegister, setIsRegister] = createSignal(false);
    const { setNavLinks } = useNav();
    const { t } = useI18n();

    onMount(() => {
        setNavLinks([]);
    });

    return (
        <div class="auth-wrapper">
            <div class="auth-section">
                <h6 class="auth-tabs">
                    <span>{t('auth.login', 'Log In')}</span>
                    <span>{t('auth.signup', 'Sign Up')}</span>
                </h6>
                <input
                    class="hidden-checkbox"
                    type="checkbox"
                    id="reg-log"
                    name="reg-log"
                    checked={isRegister()}
                    onInput={(e) => setIsRegister(e.currentTarget.checked)}
                />
                <label for="reg-log">
                    <BsArrowUpRight class="arrow" />
                </label>
                <div class="card-3d-wrap">
                    <div class="card-3d-wrapper">
                        <div class="card-front">
                            <div class="center-wrap">
                                <LoginForm />
                            </div>
                        </div>
                        <div class="card-back">
                            <div class="center-wrap">
                                <RegisterForm />
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
