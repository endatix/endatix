import React, { useCallback } from "react";
import { toast as sonnerToast } from "sonner";
import { ToastProps } from "./types";
import { Button } from "../button";
import { ToastProgress } from "./toast-progress";

function toast(toast: Omit<ToastProps, "id">) {
  return sonnerToast.custom((id) => (
    <Toast
      variant={toast.variant}
      id={id}
      title={toast.title}
      description={toast.description}
      duration={toast.duration}
      progressBar={toast.progressBar}
      button={{
        label: toast.button.label,
        onClick: toast.button.onClick,
      }}
    />
  ));
}

function Toast({
  title,
  description,
  button,
  progressBar = "reverse",
  id,
  variant,
}: ToastProps) {
  const [isPaused] = React.useState(false);
  const remainingTimeRef = React.useRef(5000);
  const UPDATE_TIME_INTERVAL = 50;
  const DURATION = 5000;

  const handleDismiss = useCallback(() => {
    setTimeout(() => {
      sonnerToast.dismiss(id);
    }, 50);
  }, [id]);

  React.useEffect(() => {
    let timeoutId: NodeJS.Timeout;

    if (!isPaused) {
      timeoutId = setInterval(() => {
        remainingTimeRef.current = Math.max(0, remainingTimeRef.current - UPDATE_TIME_INTERVAL);
      }, UPDATE_TIME_INTERVAL);
    }

    return () => clearInterval(timeoutId);
  }, [isPaused]);

  return (
    <div className="flex flex-col gap-0 justify-between items-center rounded-lg bg-white shadow-lg ring-1 ring-black/5 w-full md:max-w-[364px] relative overflow-hidden">
      <div className="flex items-center p-4 gap-2">
        <div data-icon className="">
          <svg
            xmlns="http://www.w3.org/2000/svg"
            viewBox="0 0 20 20"
            fill="currentColor"
            height="20"
            width="20"
          >
            <path
              fillRule="evenodd"
              d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.857-9.809a.75.75 0 00-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 10-1.06 1.061l2.5 2.5a.75.75 0 001.137-.089l4-5.5z"
              clipRule="evenodd"
            />
          </svg>
        </div>
        <div className="flex flex-col justify-start">
          <div className="text-sm font-medium">{title}</div>
          <div className="text-sm text-muted-foreground">{description}</div>
        </div>
        <div className="ml-5 shrink-0 rounded-md text-sm">
          <Button
            variant="secondary"
            onClick={() => {
              button.onClick();
              handleDismiss();
            }}
          >
            {button.label}
          </Button>
        </div>
      </div>
      {progressBar !== "none" && (
        <ToastProgress
          duration={DURATION}
          variant={variant}
          direction={progressBar}
          onComplete={handleDismiss}
          remainingTimeRef={remainingTimeRef}
        />
      )}
    </div>
  );
}

export { toast, Toast };
