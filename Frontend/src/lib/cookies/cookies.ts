class Cookies {
    static set = (cname: string, cvalue: string, exmin: number): void => {
        const d = new Date();
        d.setTime(d.getTime() + exmin * 60 * 1000);
        const expires = 'expires=' + d.toUTCString();
        document.cookie = `${cname}=${cvalue};${expires};path=/; SameSite=Strict; Secure`;
    };

    static get = (cname: string): string | null => {
        const name = `${cname}=`;
        const ca = document.cookie.split(';');

        for (let i = 0; i < ca.length; i++) {
            let c = ca[i];

            while (c.charAt(0) === ' ') {
                c = c.substring(1);
            }

            if (c.indexOf(name) === 0) {
                return c.substring(name.length, c.length);
            }
        }
        return null;
    };
}

export default Cookies;
