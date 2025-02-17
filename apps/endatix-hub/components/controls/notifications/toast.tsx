import { Button } from '@/components/ui/button';
import React, { useEffect, useState, useCallback } from 'react';
import { toast as sonnerToast } from 'sonner';

function toast(toast: Omit<ToastProps, 'id'>) {
  return sonnerToast.custom((id) => (
    <Toast
      variant={toast.variant}
      id={id}
      title={toast.title}
      //   duration={5000}
      description={toast.description}
      button={{
        label: toast.button.label,
        onClick: () => console.log('Button clicked'),
      }}
    />
  ));
}

interface ToastProps {
  id: string | number;
  title: string;
  duration?: number;
  description?: string;
  button: {
    label: string;
    onClick: () => void;
  };
  variant: 'success' | 'info' | 'warning' | 'error';
}

/** A fully custom toast that still maintains the animations and interactions. */
function Toast({
  title,
  description,
  button,
  duration = 5000,
  id,
}: ToastProps) {
  const [isPaused, setIsPaused] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const reverseProgressBar = false;
  const STEP_INTERVAL = 100;

  const removeToast = React.useCallback((id: string | number) => {
    setTimeout(() => {
      sonnerToast.dismiss(id);
    }, 100);
  }, []);

  const updateProgressBar = useCallback(() => {
    const interval = setInterval(() => {
      if (isPaused) return;

      setCurrentTime((currentValue) => {
        const newValue = reverseProgressBar
          ? currentValue + STEP_INTERVAL
          : currentValue - STEP_INTERVAL;

        if (!reverseProgressBar && newValue <= 0) {
          clearInterval(interval);
          removeToast(id);
          return 0;
        }

        if (reverseProgressBar && newValue >= duration) {
          clearInterval(interval);
          removeToast(id);
          return duration;
        }

        return newValue;
      });
    }, STEP_INTERVAL);

    return interval;
  }, [isPaused, reverseProgressBar, duration, id, removeToast]);

  useEffect(() => {
    const initialValue = reverseProgressBar ? 0 : duration;
    setCurrentTime(initialValue);

    const interval = updateProgressBar();

    return () => clearInterval(interval);
  }, [duration, reverseProgressBar, updateProgressBar]);

  const progressPercentage = Math.floor(100 * (currentTime / duration));

  return (
    <div
      className="flex flex-col gap-0 justify-between items-center rounded-lg bg-white shadow-lg ring-1 ring-black/5 w-full md:max-w-[364px] relative overflow-hidden"
      onMouseEnter={() => {
        setIsPaused(true);
      }}
      onMouseLeave={() => {
        setIsPaused(false);
        updateProgressBar();
      }}
    >
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
          <div className="text-sm">{description}</div>
        </div>
        <div className="ml-5 shrink-0 rounded-md text-sm">
          <Button
            variant="secondary"
            onClick={() => {
              button.onClick();
              sonnerToast.dismiss(id);
            }}
          >
            {button.label}
          </Button>
        </div>
      </div>
      <div className="relative w-full h-1.5">
        <div className="absolute bottom-0 left-0 right-0 h-full bg-green-500/20" />
        <div
          className="absolute bottom-0 left-0 h-full bg-green-500 transition-all duration-150 ease-out"
          style={{ width: `${progressPercentage}%` }}
        />
      </div>
    </div>
  );
}

export { toast, Toast };
