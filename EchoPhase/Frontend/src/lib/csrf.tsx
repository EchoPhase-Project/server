export async function getCsrfToken(): Promise<string | null> {
	const res = await fetch("/api/v1/auth/csrf", {
		method: "GET",
		credentials: "include"
	});
	return res.headers.get("X-CSRF-TOKEN");
}
