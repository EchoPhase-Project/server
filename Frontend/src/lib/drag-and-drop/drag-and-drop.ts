type DragFunction = (e: MouseEvent, el: HTMLElement, info?: DragInfo) => void;

/**
 * Configuration interface for drag and drop behavior
 */
export interface DragInfo {
    /** The element that listens for mousedown events to initiate dragging */
    currentTarget: HTMLElement;

    /** The element that will be moved/transformed during dragging */
    target: HTMLElement;

    /** Callback fired when dragging starts on currentTarget */
    currentTarget_in_f?: DragFunction;

    /** Callback fired when dragging starts on target */
    target_in_f?: DragFunction;

    /** Callback fired continuously while dragging currentTarget */
    currentTarget_f?: DragFunction;

    /** Callback fired continuously while dragging target */
    target_f?: DragFunction;

    /** Callback fired when dragging ends on currentTarget */
    currentTarget_out_f?: DragFunction;

    /** Callback fired when dragging ends on target */
    target_out_f?: DragFunction;

    /** Callback fired when element is dropped (after dragging ends) */
    drop_f?: DragFunction;

    /** Whether to allow horizontal (X-axis) movement. Default: true */
    modify_X?: boolean;

    /** Whether to allow vertical (Y-axis) movement. Default: true */
    modify_Y?: boolean;

    /** Additional X offset applied at drag start and removed at drag end */
    extraX?: number;

    /** Additional Y offset applied at drag start and removed at drag end */
    extraY?: number;
}

/**
 * Utility class for implementing drag and drop functionality with transform3d
 */
export class DragAndDrop {
    /** List of currently active drag operations */
    private static dragList: DragInfo[] = [];

    /**
     * Extracts translate3d values from an element's transform style
     * @param el - The HTML element to read transform from
     * @returns Tuple of [x, y] translation values in pixels
     */
    private static getTranslate(el: HTMLElement): [number, number] {
        const match = el.style.transform.match(
            /translate3d\(([-\d.]+)px,\s*([-\d.]+)px/
        );
        return match ? [parseFloat(match[1]), parseFloat(match[2])] : [0, 0];
    }

    /**
     * Sets translate3d transform on an element
     * @param el - The HTML element to transform
     * @param x - X-axis translation in pixels
     * @param y - Y-axis translation in pixels
     */
    private static setTranslate(el: HTMLElement, x: number, y: number): void {
        el.style.transform = `translate3d(${x}px, ${y}px, 0)`;
    }

    /**
     * Gets the list of currently active drag operations
     * @returns Array of active DragInfo objects
     */
    public static get(): DragInfo[] {
        return this.dragList;
    }

    /**
     * Binds drag and drop behavior to elements based on provided configuration
     * @param info - Configuration object defining drag behavior and callbacks
     * @returns The DragAndDrop class for method chaining
     *
     * @example
     * DragAndDrop.bind({
     *   currentTarget: handleElement,  // Element to grab
     *   target: dragElement,           // Element to move
     *   modify_X: true,                // Allow horizontal movement
     *   modify_Y: true,                // Allow vertical movement
     *   drop_f: (e, el) => {           // Handle drop
     *     console.log('Dropped at', e.clientX, e.clientY);
     *   }
     * });
     */
    public static bind(info: DragInfo): typeof DragAndDrop {
        info.currentTarget.addEventListener('mousedown', (e: MouseEvent) => {
            // Only start drag if mousedown is directly on currentTarget
            if (e.target !== e.currentTarget) return;

            // Set default values for optional parameters
            info.modify_X ??= true;
            info.modify_Y ??= true;
            info.extraX ??= 0;
            info.extraY ??= 0;

            // Add to active drag list
            this.dragList.push(info);

            // Fire drag start callbacks
            info.currentTarget_in_f?.(e, info.currentTarget, info);
            info.target_in_f?.(e, info.target, info);

            // Apply initial extra offset
            const [x, y] = this.getTranslate(info.target);
            this.setTranslate(info.target, x + info.extraX, y + info.extraY);

            /**
             * Handles mouse movement during drag
             * @param moveEvent - MouseMove event
             */
            const moveHandler = (moveEvent: MouseEvent) => {
                window.requestAnimationFrame(() => {
                    // Process all active drags
                    for (const i of this.dragList) {
                        const [cx, cy] = this.getTranslate(i.target);

                        // Calculate movement deltas based on axis restrictions
                        const dx = i.modify_X ? moveEvent.movementX : 0;
                        const dy = i.modify_Y ? moveEvent.movementY : 0;

                        /*
						const prevPointerEvents = i.target.style.pointerEvents;
						i.target.style.pointerEvents = "none";

						const elemUnderCursor = document.elementFromPoint(moveEvent.clientX, moveEvent.clientY) as HTMLElement | null;

						i.target.style.pointerEvents = prevPointerEvents;
						console.log(elemUnderCursor);
						*/

                        // Fire continuous drag callbacks
                        i.currentTarget_f?.(moveEvent, i.currentTarget, i);
                        i.target_f?.(moveEvent, i.target, i);

                        // Apply movement to target element
                        this.setTranslate(i.target, cx + dx, cy + dy);
                    }
                });
            };

            /**
             * Handles mouse up to end drag operation
             * @param upEvent - MouseUp event
             */
            const upHandler = (upEvent: MouseEvent) => {
                // Process all active drags for cleanup
                for (const i of this.dragList) {
                    // Fire drag end callbacks
                    i.currentTarget_out_f?.(upEvent, i.currentTarget, i);
                    i.target_out_f?.(upEvent, i.target, i);
                    i.drop_f?.(upEvent, i.target, i);

                    // Remove extra offset that was applied at start
                    const [cx, cy] = this.getTranslate(i.target);
                    this.setTranslate(i.target, cx - i.extraX!, cy - i.extraY!);
                }

                // Clear drag list and remove event listeners
                this.dragList = [];
                window.removeEventListener('mousemove', moveHandler);
                window.removeEventListener('mouseup', upHandler);
            };

            // Attach global mouse event listeners for drag operation
            window.addEventListener('mousemove', moveHandler);
            window.addEventListener('mouseup', upHandler);
        });

        return this;
    }
}

export default DragAndDrop;
