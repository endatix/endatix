import { ToastT } from 'sonner';

interface ToastProps {
  id: string | number;
  title: string;
  description?: string;
  toast?: ToastT;
  toasts?: ToastT[];
  duration?: number;
  index?: number;
  progressBar?: "none" | "normal" | "reverse";
  variant: "success" | "info" | "warning" | "error";
  button: {
    label: string;
    onClick: () => void;
  };
}

export type { ToastProps };
