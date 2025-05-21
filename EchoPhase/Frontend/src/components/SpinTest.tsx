import { onMount } from "solid-js";
import SpinEffect from "../lib/spin-effect";

export default function SpinTestPage() {
  let cardRef: HTMLDivElement | undefined;

  onMount(() => {
    if (cardRef) {
      SpinEffect.add({
        element: cardRef,
        intensity: 15,
        scale: 1.08,
        mousemove_f: (e, el) => {
          console.log("mousemove", el);
        },
        mouseleave_f: (e, el) => {
          console.log("mouseleave", el);
        }
      });
    }
  });

  return (
    <div style={{ display: "flex", "justify-content": "center", "align-items": "center", height: "100vh" }}>
      <div
        ref={cardRef}
        style={{
          width: "300px",
          height: "300px",
          "background-color": "lightcoral",
          "border-radius": "12px",
          "box-shadow": "0 8px 20px rgba(0,0,0,0.2)",
          display: "flex",
          "align-items": "center",
          "justify-content": "center",
          color: "#fff",
          "font-size": "24px",
          "font-weight": "bold",
          cursor: "pointer"
        }}
      >
        Hover Me
      </div>
    </div>
  );
}

