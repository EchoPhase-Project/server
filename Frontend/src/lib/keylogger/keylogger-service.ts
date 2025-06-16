/**
 * Handler definition for a key combination.
 */
type KeyComboHandler = {
    /** Array of key codes (e.g. 'KeyA', 'ShiftLeft') that define the combination */
    keyCodes: string[];
    /**
     * Function to execute when the key combination is detected.
     * @param e - The keyboard event that triggered the handler.
     * @param handler - The KeyComboHandler object itself.
     */
    func: (e: KeyboardEvent, handler: KeyComboHandler) => void;
};

/**
 * Service to track pressed keys and handle key combinations.
 * It listens to global keyboard events and triggers handlers on keyup.
 */
export class KeyloggerService {
    private static pressedKeys: Set<string> = new Set();
    private static handlers: KeyComboHandler[] = [];
    private static keydownListener: (e: KeyboardEvent) => void;
    private static keyupListener: (e: KeyboardEvent) => void;
    private static mouseleaveListener: (e: MouseEvent) => void;

    /** Enable or disable debug logging */
    static Debug: boolean = false;

    static {
        this.mouseleaveListener = () => this.clear();

        this.keydownListener = (e: KeyboardEvent) => {
            if (this.Debug) console.log('keydown', e.code);
            this.pressedKeys.add(e.code);
        };

        this.keyupListener = (e: KeyboardEvent) => {
            if (this.Debug) console.log('keyup', e.code);
            this.pressedKeys.delete(e.code);

            for (const handler of this.handlers) {
                if (handler.keyCodes.every((k) => this.pressedKeys.has(k))) {
                    handler.func(e, handler);
                }
            }
        };

        window.addEventListener('keydown', this.keydownListener);
        window.addEventListener('keyup', this.keyupListener);
        window.addEventListener('mouseleave', this.mouseleaveListener);
    }

    /**
     * Register a new key combination handler.
     * @param keyCodes - Array of key codes defining the combo.
     * @param func - Callback function to execute on combo detection.
     * @returns The KeyloggerService class for chaining.
     */
    static add(
        keyCodes: string[],
        func: (e: KeyboardEvent, handler: KeyComboHandler) => void
    ): typeof KeyloggerService {
        if (!Array.isArray(keyCodes) || keyCodes.length === 0) {
            throw new Error('keyCodes cannot be empty');
        }

        this.handlers.push({ keyCodes, func });
        return this;
    }

    /**
     * Remove a registered key combination handler.
     * @param keyCodes - Array of key codes defining the combo to remove.
     * @param func - Optional specific callback function to remove (if omitted, removes all handlers matching keyCodes).
     * @returns The KeyloggerService class for chaining.
     */
    static remove(
        keyCodes: string[],
        func?: (e: KeyboardEvent, handler: KeyComboHandler) => void
    ): typeof KeyloggerService {
        this.handlers = this.handlers.filter((h) => {
            const sameKeys =
                h.keyCodes.length === keyCodes.length &&
                h.keyCodes.every((k) => keyCodes.includes(k));
            const sameFunc = !func || h.func === func;
            return !(sameKeys && sameFunc);
        });
        return this;
    }

    /**
     * Get currently pressed keys as an array of key codes.
     * @returns Array of pressed key codes.
     */
    static getKeys(): string[] {
        return Array.from(this.pressedKeys);
    }

    /**
     * Clear all currently pressed keys.
     * @returns The KeyloggerService class for chaining.
     */
    static clear(): typeof KeyloggerService {
        this.pressedKeys.clear();
        return this;
    }

    /**
     * Remove all event listeners and clear all state.
     * Use to clean up the service when no longer needed.
     */
    static destroy(): void {
        window.removeEventListener('keydown', this.keydownListener);
        window.removeEventListener('keyup', this.keyupListener);
        window.removeEventListener('mouseleave', this.mouseleaveListener);

        this.pressedKeys.clear();
        this.handlers = [];
    }
}

export default KeyloggerService;
