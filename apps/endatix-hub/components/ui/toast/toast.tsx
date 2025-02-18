import React, { useCallback } from "react";
import { toast as sonnerToast } from "sonner";
import { ToastProps } from "./types";
import { Button } from "../button";
import { ToastProgress } from "./toast-progress";
import { ToastIcon } from "./toast-icon";

const DEFAULT_DURATION = 5000;

const DEFAULT_TOAST_PROPS: Omit<ToastProps, "id" | "title"> = {
  variant: "info",
  duration: DEFAULT_DURATION,
  progressBar: "reverse",
  description: undefined,
  includeIcon: true,
  action: undefined,
  SvgIcon: undefined,
};

function toast(toast: Omit<ToastProps, "id">) {
  const mergedProps = {
    ...DEFAULT_TOAST_PROPS,
    ...toast,
  };

  return sonnerToast.custom((id) => <Toast {...mergedProps} id={id} />);
}

function Toast({
  title,
  description,
  duration,
  action,
  progressBar = "reverse",
  id,
  variant,
  SvgIcon,
  includeIcon,
}: ToastProps) {
  const [isPaused] = React.useState(false);
  const remainingTimeRef = React.useRef(duration ?? DEFAULT_DURATION);
  const UPDATE_TIME_INTERVAL = 50;

  const handleDismiss = useCallback(() => {
    setTimeout(() => {
      sonnerToast.dismiss(id);
    }, UPDATE_TIME_INTERVAL);
  }, [id]);

  React.useEffect(() => {
    let timeoutId: NodeJS.Timeout;

    if (!isPaused) {
      timeoutId = setInterval(() => {
        remainingTimeRef.current = Math.max(
          0,
          remainingTimeRef.current - UPDATE_TIME_INTERVAL,
        );
      }, UPDATE_TIME_INTERVAL);
    }

    return () => clearInterval(timeoutId);
  }, [isPaused]);

  return (
    <div className="flex flex-col w-full min-w-[356px] md:max-w-[364px] gap-0 justify-between items-center rounded-lg bg-white shadow-lg ring-1 ring-black/5 relative overflow-hidden">
      <div className="flex flex-row justify-between w-full p-4 gap-2">
        {includeIcon && (
          <ToastIcon
            variant={variant}
            SvgIcon={SvgIcon}
            includeIcon={includeIcon}
          />
        )}
        <div className="flex flex-col justify-start w-full">
          <div className="text-sm font-medium">{title}</div>
          <div className="text-sm text-muted-foreground">{description}</div>
        </div>
        {action && (
          <div className="flex ml-5 items-center text-sm">
            <Button
              variant="secondary"
              onClick={() => {
                action.onClick();
                handleDismiss();
              }}
            >
              {action.label}
            </Button>
          </div>
        )}
      </div>
      {progressBar !== "none" && (
        <ToastProgress
          duration={duration ?? DEFAULT_DURATION}
          variant={variant}
          direction={"reverse"}
          onComplete={handleDismiss}
          remainingTimeRef={remainingTimeRef}
        />
      )}
    </div>
  );
}

export { toast, Toast };
