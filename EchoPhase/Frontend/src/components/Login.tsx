import { createSignal, onMount } from "solid-js";
import { getCsrfToken } from "../lib/csrf";

export default function Login() {
	const [username, setUsername] = createSignal("");
	const [password, setPassword] = createSignal("");
	const [csrfToken, setCsrfToken] = createSignal<string | null>(null);

	onMount(async () => {
		const token = await getCsrfToken();
		setCsrfToken(token);
	});

	async function handleLogin() {
		const res = await fetch("/api/auth/login", {
			method: "POST",
			credentials: "include",
			headers: {
				"Content-Type": "application/json",
				...(csrfToken() ? { "X-CSRF-TOKEN": csrfToken() } : {})
			},
			body: JSON.stringify({
				username: username(),
				password: password()
			})
		});
		alert(res.ok ? "Logged in!" : "Login failed");
	}

	return (
		<div>
			<h2>Login</h2>
			<input placeholder="Username" onInput={e => setUsername(e.currentTarget.value)} />
			<input type="password" placeholder="Password" onInput={e => setPassword(e.currentTarget.value)} />
			<button onClick={handleLogin}>Login</button>
		</div>
	);
}

