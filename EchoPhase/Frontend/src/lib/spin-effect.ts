export type SpinFunction = (e: MouseEvent, el: HTMLElement, info?: SpinInfo) => void;

export interface SpinInfo {
  element: HTMLElement;
  intensity?: number;
  scale?: number;
  mousemove_f?: SpinFunction;
  mouseleave_f?: SpinFunction;
  __moveHandler__?: EventListener;
  __leaveHandler__?: EventListener;
  __enterHandler__?: EventListener;
}

export class SpinEffect {
  private static spinList: SpinInfo[] = [];

  private static applyTransform(
    el: HTMLElement,
    rotateX: number,
    rotateY: number,
    scale: number
  ) {
    el.style.transform = `perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) scale3d(${scale}, ${scale}, 1)`;
  }

  public static clear(): typeof SpinEffect {
    for (const info of [...this.spinList]) {
      this.remove(info);
    }
    return this;
  }

  public static remove(info: SpinInfo): typeof SpinEffect {
    if (info.__moveHandler__) {
      info.element.removeEventListener("mousemove", info.__moveHandler__);
    }
    if (info.__leaveHandler__) {
      info.element.removeEventListener("mouseleave", info.__leaveHandler__);
    }
    if (info.__enterHandler__) {
      info.element.removeEventListener("mouseenter", info.__enterHandler__);
    }
    info.element.style.transform = "";
    this.spinList = this.spinList.filter(i => i !== info);
    return this;
  }

  public static add(info: SpinInfo): typeof SpinEffect {
    const el = info.element;
    const intensity = info.intensity ?? 10;
    const scale = info.scale ?? 1.05;

    let frameId = 0;

    const moveHandler = (e: MouseEvent) => {
      if (frameId) cancelAnimationFrame(frameId);
      frameId = requestAnimationFrame(() => {
        const rect = el.getBoundingClientRect();
        const offsetX = e.clientX - rect.left;
        const offsetY = e.clientY - rect.top;

        const rotateX = ((offsetY - rect.height / 2) / rect.height) * intensity * -1; // -rotateX
        const rotateY = ((offsetX - rect.width / 2) / rect.width) * intensity;

        this.applyTransform(el, rotateX, rotateY, scale);
        info.mousemove_f?.(e, el, info);
      });
    };

    const leaveHandler = (e: MouseEvent) => {
      this.applyTransform(el, 0, 0, 1);
      info.mouseleave_f?.(e, el, info);

      el.removeEventListener("mousemove", moveHandler);
      el.removeEventListener("mouseleave", leaveHandler);
    };

    const enterHandler = (e: MouseEvent) => {
      el.addEventListener("mousemove", moveHandler);
      el.addEventListener("mouseleave", leaveHandler);
    };

    info.__moveHandler__ = moveHandler as EventListener;
    info.__leaveHandler__ = leaveHandler as EventListener;
    info.__enterHandler__ = enterHandler as EventListener;

    el.addEventListener("mouseenter", enterHandler);

    this.spinList.push(info);
    return this;
  }
}

export default SpinEffect;
