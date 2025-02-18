import { useEffect, useState, RefObject } from "react";
import { ToastProps } from "./types";

interface ToastProgressProps {
  duration: number;
  variant: ToastProps["variant"];
  direction: "left-to-right" | "right-to-left";
  onComplete: () => void;
  isPaused: boolean;
  remainingTimeRef: RefObject<number>;
}

function ToastProgress({
  duration,
  variant,
  direction,
  onComplete,
  remainingTimeRef,
  isPaused,
}: ToastProgressProps) {
  const [displayTime, setDisplayTime] = useState(duration);
  const UI_UPDATE_INTERVAL = 25;

  useEffect(() => {
    if (isPaused) return;

    const interval = setInterval(() => {
      const currentRemaining = remainingTimeRef.current;
      setDisplayTime(currentRemaining);

      if (currentRemaining <= 0) {
        onComplete();
      }
    }, UI_UPDATE_INTERVAL);

    return () => clearInterval(interval);
  }, [isPaused, remainingTimeRef, onComplete]);

  const progressPercentage =
    direction === "left-to-right"
      ? Math.floor(100 * ((duration - displayTime) / duration))
      : Math.floor(100 * (displayTime / duration));

  const variantColors = {
    success: "bg-green-500",
    error: "bg-red-500",
    warning: "bg-yellow-500",
    info: "bg-blue-500",
  };

  const baseColor = variantColors[variant];

  return (
    <div className="relative w-full h-1.5">
      <div
        className={`absolute bottom-0 left-0 right-0 h-full w-full ${baseColor} opacity-10`}
      />
      <div
        className={`absolute bottom-0 left-0 h-full w-full ${baseColor} transition-transform duration-300 ease-linear`}
        style={{
          transform: `translateX(${progressPercentage - 100}%)`,
          width: "100%",
        }}
      />
    </div>
  );
}

export { ToastProgress };
