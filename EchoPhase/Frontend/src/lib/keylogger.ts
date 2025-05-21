type KeyComboHandler = {
	keyCodes: string[];
	func: (e: KeyboardEvent, handler: KeyComboHandler) => void;
};

export class Keylogger {
	private static pressedKeys: Set<string> = new Set();
	private static handlers: KeyComboHandler[] = [];
	private static keydownListener: (e: KeyboardEvent) => void;
	private static keyupListener: (e: KeyboardEvent) => void;
	private static mouseleaveListener: (e: MouseEvent) => void;
	static Debug: boolean = false;

	static {
		this.mouseleaveListener = () => this.clear();

		this.keydownListener = (e: KeyboardEvent) => {
			if (this.Debug) console.log("keydown", e.code);

			this.pressedKeys.add(e.code);
		};

		this.keyupListener = (e: KeyboardEvent) => {
			if (this.Debug) console.log("keyup", e.code);

			this.pressedKeys.delete(e.code);

			for (const handler of this.handlers) {
				if (handler.keyCodes.every(k => this.pressedKeys.has(k))) {
					handler.func(e, handler);
				}
			}
		};

		window.addEventListener("keydown", this.keydownListener);
		window.addEventListener("keyup", this.keyupListener);
		window.addEventListener("mouseleave", this.mouseleaveListener);
	}

	static add(keyCodes: string[], func: (e: KeyboardEvent, handler: KeyComboHandler) => void): typeof Keylogger {
		if (!Array.isArray(keyCodes) || keyCodes.length === 0) {
			throw new Error("keyCodes cannot be empty");
		}

		this.handlers.push({ keyCodes, func });
		return this;
	}

	static remove(keyCodes: string[], func?: (e: KeyboardEvent, handler: KeyComboHandler) => void): typeof Keylogger {
		this.handlers = this.handlers.filter(h => {
			const sameKeys = h.keyCodes.length === keyCodes.length &&
				h.keyCodes.every(k => keyCodes.includes(k));
			const sameFunc = !func || h.func === func;
			return !(sameKeys && sameFunc);
		});
		return this;
	}

	static getKeys(): string[] {
		return Array.from(this.pressedKeys);
	}

	static clear(): typeof Keylogger {
		this.pressedKeys.clear();
		return this;
	}

	static destroy(): void {
		window.removeEventListener("keydown", this.keydownListener);
		window.removeEventListener("keyup", this.keyupListener);
		window.removeEventListener("mouseleave", this.mouseleaveListener);

		this.pressedKeys.clear();
		this.handlers = [];
	}
}

export default Keylogger;
