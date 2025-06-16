export class DateHelper {
    /**
     * Returns the expiration date after the specified number of minutes from now.
     * @param minutes Number of minutes until expiration
     */
    static expiresInMinutes(minutes: number): Date {
        const d = new Date();
        d.setTime(d.getTime() + minutes * 60 * 1000);
        return d;
    }

    /**
     * Returns the expiration date after the specified number of hours from now.
     * @param hours Number of hours until expiration
     */
    static expiresInHours(hours: number): Date {
        const d = new Date();
        d.setTime(d.getTime() + hours * 60 * 60 * 1000);
        return d;
    }

    /**
     * Returns the expiration date after the specified number of days from now.
     * @param days Number of days until expiration
     */
    static expiresInDays(days: number): Date {
        const d = new Date();
        d.setTime(d.getTime() + days * 24 * 60 * 60 * 1000);
        return d;
    }

    /**
     * Returns the expiration date after the specified number of seconds from now.
     * @param seconds Number of seconds until expiration
     */
    static expiresInSeconds(seconds: number): Date {
        const d = new Date();
        d.setTime(d.getTime() + seconds * 1000);
        return d;
    }
}

export default DateHelper;
