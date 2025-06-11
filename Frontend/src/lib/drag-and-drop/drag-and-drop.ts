type DragFunction = (e: MouseEvent, el: HTMLElement, info?: DragInfo) => void;

export interface DragInfo {
    currentTarget: HTMLElement;
    target: HTMLElement;
    currentTarget_in_f?: DragFunction;
    target_in_f?: DragFunction;
    currentTarget_f?: DragFunction;
    target_f?: DragFunction;
    currentTarget_out_f?: DragFunction;
    target_out_f?: DragFunction;
    drop_f?: DragFunction;
    modify_X?: boolean;
    modify_Y?: boolean;
    extraX?: number;
    extraY?: number;
}

export class DragAndDrop {
    private static dragList: DragInfo[] = [];

    private static getTranslate(el: HTMLElement): [number, number] {
        const match = el.style.transform.match(
            /translate3d\(([-\d.]+)px,\s*([-\d.]+)px/
        );
        return match ? [parseFloat(match[1]), parseFloat(match[2])] : [0, 0];
    }

    private static setTranslate(el: HTMLElement, x: number, y: number): void {
        el.style.transform = `translate3d(${x}px, ${y}px, 0)`;
    }

    public static get(): DragInfo[] {
        return this.dragList;
    }

    public static bind(info: DragInfo): typeof DragAndDrop {
        info.currentTarget.addEventListener('mousedown', (e: MouseEvent) => {
            if (e.target !== e.currentTarget) return;

            info.modify_X ??= true;
            info.modify_Y ??= true;
            info.extraX ??= 0;
            info.extraY ??= 0;

            this.dragList.push(info);

            info.currentTarget_in_f?.(e, info.currentTarget, info);
            info.target_in_f?.(e, info.target, info);

            const [x, y] = this.getTranslate(info.target);
            this.setTranslate(info.target, x + info.extraX, y + info.extraY);

            const moveHandler = (moveEvent: MouseEvent) => {
                window.requestAnimationFrame(() => {
                    for (const i of this.dragList) {
                        const [cx, cy] = this.getTranslate(i.target);
                        const dx = i.modify_X ? moveEvent.movementX : 0;
                        const dy = i.modify_Y ? moveEvent.movementY : 0;

                        i.currentTarget_f?.(moveEvent, i.currentTarget, i);
                        i.target_f?.(moveEvent, i.target, i);

                        /*
						const prevPointerEvents = i.target.style.pointerEvents;
						i.target.style.pointerEvents = "none";

						const elemUnderCursor = document.elementFromPoint(moveEvent.clientX, moveEvent.clientY) as HTMLElement | null;

						i.target.style.pointerEvents = prevPointerEvents;
						console.log(elemUnderCursor);
						*/

                        this.setTranslate(i.target, cx + dx, cy + dy);
                    }
                });
            };

            const upHandler = (upEvent: MouseEvent) => {
                for (const i of this.dragList) {
                    i.currentTarget_out_f?.(upEvent, i.currentTarget, i);
                    i.target_out_f?.(upEvent, i.target, i);
                    i.drop_f?.(upEvent, i.target, i);

                    const [cx, cy] = this.getTranslate(i.target);
                    this.setTranslate(i.target, cx - i.extraX!, cy - i.extraY!);
                }

                this.dragList = [];

                window.removeEventListener('mousemove', moveHandler);
                window.removeEventListener('mouseup', upHandler);
            };

            window.addEventListener('mousemove', moveHandler);
            window.addEventListener('mouseup', upHandler);
        });

        return this;
    }
}

export default DragAndDrop;
