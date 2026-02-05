import {
    createContext,
    useContext,
    onMount,
    onCleanup,
    ParentComponent,
} from 'solid-js';

/**
 * Cursor position on the page
 */
interface CursorPosition {
    x: number;
    y: number;
}

/**
 * Configuration options for cursor interaction with elements
 */
interface InteractOptions {
    /** Cursor configuration */
    cursor: {
        /** Size multiplier relative to the element (e.g., 1.2 = 120% of element size) */
        sizeRate?: number;
        /** If true, cursor will "grab" and follow the element */
        grab?: boolean;
        /** List of CSS classes to add to cursor on hover */
        classList?: string[];
    };
    /** Target element configuration */
    target: {
        /** Movement coefficient on hover (0-1, where 1 = full follow) */
        movementRate: number;
        /** List of CSS classes to add to element on hover */
        classList?: string[];
    };
    /** Interaction area radius (default 20px) */
    coordination?: number;
    /** If true, element will be centered relative to its position */
    centrify?: boolean;
    /** Additional CSS transforms for the element */
    transforms?: string[];
    /** Mouse event callbacks */
    callbacks?: {
        /** Called when mouse enters the element */
        over?: (e: MouseEvent) => void;
        /** Called when mouse moves over the element */
        move?: (e: MouseEvent) => void;
        /** Called when mouse leaves the element */
        out?: (e: MouseEvent) => void;
    };
}

/**
 * Custom cursor service interface
 */
interface ICursorService {
    /** Focuses the cursor (e.g., on click) */
    focus: () => void;
    /** Unfocuses the cursor */
    unfocus: () => void;
    /** Sets up interactive behavior for elements matching the selector */
    interact: (
        elementSelector: string,
        options: InteractOptions
    ) => ICursorService;
}

/**
 * Custom cursor management service
 * Handles mouse position tracking, DOM synchronization and interactive effects
 */
class CursorService implements ICursorService {
    /** Current cursor X coordinate (may differ from actual mouse during interaction) */
    private cursorX: number = 0;
    /** Current cursor Y coordinate (may differ from actual mouse during interaction) */
    private cursorY: number = 0;
    /** Actual mouse X coordinate */
    private mouseX: number = 0;
    /** Actual mouse Y coordinate */
    private mouseY: number = 0;

    /** Custom cursor DOM element */
    private cursor: HTMLElement | null = null;
    /** Cursor container DOM element */
    private cursorContainer: HTMLElement | null = null;

    /**
     * Initializes the cursor service
     * Finds necessary DOM elements and sets up event handlers
     */
    initialize(): void {
        this.cursor = document.querySelector<HTMLElement>('#cursor');
        this.cursorContainer =
            document.querySelector<HTMLElement>('#cursor-container');

        if (this.cursorContainer && this.cursor) {
            this.show();
            this.attachEventListeners();
        } else {
            this.hide();
        }
    }

    /**
     * Cleans up resources and removes event handlers
     */
    cleanup(): void {
        this.removeEventListeners();
    }

    /**
     * Synchronizes cursor position with current mouse position
     * Takes page scroll into account
     * @private
     */
    private sync = (): void => {
        if (!this.cursorContainer) return;

        this.cursorContainer.style.transform = `translate3d(${window.scrollX + this.mouseX}px, ${window.scrollY + this.mouseY}px, 0)`;
    };

    /**
     * Mouse move event handler
     * Updates buffered position and moves cursor
     * @param e - Mouse move event
     * @private
     */
    private move = (e: MouseEvent): void => {
        this.mouseX = this.cursorX = e.clientX;
        this.mouseY = this.cursorY = e.clientY;

        // Don't update position if cursor is interacting with an element
        if (this.cursor?.classList.contains('cursor-interaction')) {
            return;
        }

        this.sync();
    };

    /**
     * Page scroll event handler
     * Unfocuses cursor and synchronizes position
     * @private
     */
    private scroll = (): void => {
        this.unfocus();
        this.sync();
    };

    /**
     * Shows the cursor
     * @private
     */
    private show = (): void => {
        if (!this.cursorContainer) return;

        this.cursorContainer.classList.toggle('cursor-hidden', false);
        this.cursorContainer.classList.add('cursor-visible');
    };

    /**
     * Hides the cursor
     * @private
     */
    private hide = (): void => {
        if (!this.cursorContainer) return;

        this.cursorContainer.classList.toggle('cursor-visible', false);
        this.cursorContainer.classList.add('cursor-hidden');
    };

    /**
     * Focuses the cursor (adds cursor-focus class)
     * Usually called on mouse button press
     */
    focus = (): void => {
        if (!this.cursor) return;
        this.cursor.classList.add('cursor-focus');
    };

    /**
     * Unfocuses the cursor and resets all modifications
     * Removes added classes, resets size and position
     */
    unfocus = (): void => {
        if (!this.cursorContainer || !this.cursor) return;

        // Filter container classes, keeping only base ones
        this.cursorContainer.classList.value = Array.from(
            this.cursorContainer.classList
        )
            .filter((cls) => ['cursor-visible'].includes(cls))
            .join(' ');

        // Clear all cursor classes
        this.cursor.classList.value = Array.from(this.cursor.classList)
            .filter((cls) => [].includes(cls))
            .join(' ');

        // Reset custom cursor size
        this.cursor.style.width = '';
        this.cursor.style.height = '';

        // Return cursor position to actual mouse position
        this.cursorX = this.mouseX;
        this.cursorY = this.mouseY;
    };

    /**
     * Sets up interactive cursor behavior with elements
     *
     * @param elementSelector - CSS selector for elements to interact with
     * @param options - Interaction configuration
     * @returns Current service instance for method chaining
     *
     * @example
     * ```tsx
     * cursor.interact('.card', {
     *   cursor: { sizeRate: 1.2, grab: true },
     *   target: { movementRate: 0.3 },
     *   centrify: true
     * });
     * ```
     */
    interact(elementSelector: string, options: InteractOptions): this {
        const currentElements =
            document.querySelectorAll<HTMLElement>(elementSelector);

        currentElements.forEach((item) => {
            /**
             * Mouse enter element handler
             * Changes cursor size and applies styles
             */
            const handleMouseOver = (e: MouseEvent): void => {
                if (this.cursor?.classList.contains('cursor-focus')) return;

                const target = e.currentTarget as HTMLElement;
                const position = target.getBoundingClientRect();

                // Set cursor position to element center
                this.cursorX =
                    window.scrollX + position.left + position.width * 0.5;
                this.cursorY =
                    window.scrollY + position.top + position.height * 0.5;

                // Scale cursor size proportionally to element size
                if (options.cursor.sizeRate && this.cursor) {
                    this.cursor.style.width = `${position.width * options.cursor.sizeRate}px`;
                    this.cursor.style.height = `${position.height * options.cursor.sizeRate}px`;
                }

                // Activate element "grab" mode
                if (
                    options.cursor.grab &&
                    this.cursor &&
                    this.cursorContainer
                ) {
                    this.cursor.classList.add('cursor-interaction');
                    this.cursorContainer.style.transform = `translate3d(${this.cursorX}px, ${this.cursorY}px, 0)`;
                }

                // Add custom classes to element
                if (options.target.classList) {
                    options.target.classList.forEach((cls) =>
                        target.classList.add(cls)
                    );
                }

                // Add custom classes to cursor
                if (options.cursor.classList && this.cursor) {
                    options.cursor.classList.forEach((cls) =>
                        this.cursor!.classList.add(cls)
                    );
                }

                // Call user callback
                options.callbacks?.over?.(e);
            };

            /**
             * Mouse move over element handler
             * Creates parallax effect and cursor following
             */
            const handleMouseMove = (e: MouseEvent): void => {
                if (this.cursor?.classList.contains('cursor-focus')) return;

                const target = e.currentTarget as HTMLElement;
                const position = target.getBoundingClientRect();
                const defaultCoordination = 20;

                // Update base cursor position
                this.cursorX =
                    window.scrollX + position.left + position.width * 0.5;
                this.cursorY =
                    window.scrollY + position.top + position.height * 0.5;

                // Calculate relative mouse coordinates inside element (-coordination...+coordination)
                const coordinateX = Math.floor(
                    ((e.clientX - position.left) / position.width - 0.5) *
                        (options.coordination ?? defaultCoordination)
                );
                const coordinateY = Math.floor(
                    ((e.clientY - position.top) / position.height - 0.5) *
                        (options.coordination ?? defaultCoordination)
                );

                // Apply offset to cursor in "grab" mode
                if (options.cursor.grab && this.cursorContainer) {
                    this.cursorContainer.style.transform = `translate(${coordinateX}%, ${coordinateY}%) translate3d(${this.cursorX}px, ${this.cursorY}px, 0)`;
                }

                // Collect all transforms for element
                let transform = '';

                if (options.transforms) {
                    transform += options.transforms.join(' ') + ' ';
                }

                // Apply parallax effect to element
                const baseOffset = options.centrify ? -50 : 0;
                transform += `translate(${baseOffset + coordinateX * options.target.movementRate}%, ${baseOffset + coordinateY * options.target.movementRate}%)`;

                target.style.transform = transform;

                // Call user callback
                options.callbacks?.move?.(e);
            };

            /**
             * Mouse leave element handler
             * Resets all effects and returns element to initial state
             */
            const handleMouseOut = (e: MouseEvent): void => {
                // Disable "grab" mode
                if (options.cursor.grab && this.cursor) {
                    this.cursor.classList.remove('cursor-interaction');
                }

                if (this.cursor?.classList.contains('cursor-focus')) return;

                const target = e.currentTarget as HTMLElement;

                // Reset cursor size
                if (this.cursor) {
                    this.cursor.style.width = '';
                    this.cursor.style.height = '';
                }

                // Reset element transforms
                target.style.transform = '';

                // Remove custom classes from element
                if (options.target.classList) {
                    options.target.classList.forEach((cls) =>
                        target.classList.remove(cls)
                    );
                }

                // Remove custom classes from cursor
                if (options.cursor.classList && this.cursor) {
                    options.cursor.classList.forEach((cls) =>
                        this.cursor!.classList.remove(cls)
                    );
                }

                // Call user callback
                options.callbacks?.out?.(e);
            };

            // Attach handlers to element
            item.addEventListener('mouseover', handleMouseOver);
            item.addEventListener('mousemove', handleMouseMove);
            item.addEventListener('mouseout', handleMouseOut);
        });

        return this;
    }

    /**
     * Attaches global event handlers
     * @private
     */
    private attachEventListeners(): void {
        window.addEventListener('mousemove', this.move);
        window.addEventListener('scroll', this.scroll);
        window.addEventListener('mousedown', this.focus);
        window.addEventListener('mouseup', this.unfocus);
        document.body.addEventListener('mouseenter', this.show);
        document.body.addEventListener('mouseleave', this.hide);
    }

    /**
     * Removes global event handlers
     * @private
     */
    private removeEventListeners(): void {
        window.removeEventListener('mousemove', this.move);
        window.removeEventListener('scroll', this.scroll);
        window.removeEventListener('mousedown', this.focus);
        window.removeEventListener('mouseup', this.unfocus);
        document.body.removeEventListener('mouseenter', this.show);
        document.body.removeEventListener('mouseleave', this.hide);
    }
}

/**
 * Context for providing cursor service throughout the application
 */
const CursorContext = createContext<ICursorService>();

/**
 * Provider for custom cursor management
 * Wraps the application and provides access to cursor service
 *
 * @example
 * ```tsx
 * <CursorProvider>
 *   <App />
 * </CursorProvider>
 * ```
 */
export const CursorProvider: ParentComponent = (props) => {
    const cursorService = new CursorService();

    onMount(() => {
        cursorService.initialize();
    });

    onCleanup(() => {
        cursorService.cleanup();
    });

    return (
        <CursorContext.Provider value={cursorService}>
            {props.children}
        </CursorContext.Provider>
    );
};

/**
 * Hook for accessing cursor service from components
 *
 * @returns Cursor service instance
 * @throws Error if hook is used outside CursorProvider
 *
 * @example
 * ```tsx
 * import { useCursor } from '@lib/cursor';
 *
 * function MyComponent() {
 *   const cursor = useCursor();
 *
 *   onMount(() => {
 *     cursor.interact('.card', {
 *       cursor: {
 *         grab: true,
 *         sizeRate: 1.2,
 *         classList: ['cursor-hover']
 *       },
 *       target: {
 *         movementRate: 0.3,
 *         classList: ['card-active']
 *       },
 *       centrify: true
 *     });
 *   });
 *
 *   return <div class="card">Interactive Card</div>;
 * }
 * ```
 */
export function useCursor(): ICursorService {
    const context = useContext(CursorContext);

    if (!context) {
        throw new Error('useCursor must be used within CursorProvider');
    }

    return context;
}

/**
 * Export types for use in the application
 */
export type { InteractOptions, CursorPosition, ICursorService };
