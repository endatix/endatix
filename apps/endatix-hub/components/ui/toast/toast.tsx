import React, { useCallback } from 'react';
import { toast as sonnerToast } from 'sonner';
import { ToastProps } from './types';
import { Button } from '../button';
import { ToastProgress } from './toast-progress';
import { ToastIcon } from './toast-icon';

const DEFAULT_DURATION = 5000;

const DEFAULT_TOAST_PROPS: Omit<ToastProps, 'id' | 'title'> = {
  variant: 'info',
  duration: DEFAULT_DURATION,
  progressBar: 'right-to-left',
  description: undefined,
  includeIcon: true,
  action: undefined,
  SvgIcon: undefined,
};

function createToast(toast: Omit<ToastProps, 'id'>) {
  const mergedProps = {
    ...DEFAULT_TOAST_PROPS,
    ...toast,
  };

  return sonnerToast.custom((id) => <Toast {...mergedProps} id={id} />);
}

const toast = Object.assign(createToast, {
  success: (props: string | Omit<Omit<ToastProps, 'id'>, 'variant'>) =>
    createToast({ 
      ...(typeof props === 'string' ? { title: props } : props), 
      variant: 'success' 
    }),
  error: (props: string | Omit<Omit<ToastProps, 'id'>, 'variant'>) =>
    createToast({ 
      ...(typeof props === 'string' ? { title: props } : props), 
      variant: 'error' 
    }),
  warning: (props: string | Omit<Omit<ToastProps, 'id'>, 'variant'>) =>
    createToast({ 
      ...(typeof props === 'string' ? { title: props } : props), 
      variant: 'warning' 
    }),
  info: (props: string | Omit<Omit<ToastProps, 'id'>, 'variant'>) =>
    createToast({ 
      ...(typeof props === 'string' ? { title: props } : props), 
      variant: 'info' 
    }),
});

function Toast({
  id,
  title,
  description,
  duration,
  action,
  progressBar,
  variant,
  SvgIcon,
  includeIcon,
}: ToastProps) {
  const [isPaused, setIsPaused] = React.useState(false);
  const lastUpdatedRef = React.useRef(Date.now());
  const remainingTimeRef = React.useRef(duration ?? DEFAULT_DURATION);
  const UPDATE_TIME_INTERVAL = 50;
  const buttonProps = action ? { ...action } : {};

  const handleDismiss = useCallback(() => {
    setTimeout(() => {
      sonnerToast.dismiss(id);
    }, UPDATE_TIME_INTERVAL);
  }, [id]);

  const handlePause = useCallback(() => {
    lastUpdatedRef.current = Date.now();
    setIsPaused(true);
  }, []);

  const handleResume = useCallback(() => {
    lastUpdatedRef.current = Date.now();
    setIsPaused(false);
  }, []);

  React.useEffect(() => {
    let timeoutId: NodeJS.Timeout;

    if (!isPaused) {
      timeoutId = setInterval(() => {
        const now = Date.now();
        const timePassed = now - lastUpdatedRef.current;
        lastUpdatedRef.current = now;
        
        remainingTimeRef.current = Math.max(
          0,
          remainingTimeRef.current - timePassed
        );
      }, UPDATE_TIME_INTERVAL);
    }

    return () => clearInterval(timeoutId);
  }, [isPaused]);

  return (
    <div 
      className="flex flex-col w-full min-w-[356px] md:max-w-[364px] gap-0 justify-between items-center rounded-lg bg-white shadow-lg ring-1 ring-black/5 relative overflow-hidden"
      onMouseEnter={handlePause}
      onMouseLeave={handleResume}
    >
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
              size="sm"
              variant="outline"
              {...buttonProps}
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
      {progressBar !== 'none' && (
        <ToastProgress
          duration={duration ?? DEFAULT_DURATION}
          variant={variant}
          direction={progressBar ?? 'left-to-right'}
          onComplete={handleDismiss}
          remainingTimeRef={remainingTimeRef}
          isPaused={isPaused}
        />
      )}
    </div>
  );
}

export { toast };
