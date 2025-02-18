type ToastVariant = "success" | "info" | "warning" | "error";

interface ToastProps {
  id: string | number;
  title: string;
  description?: string;
  duration?: number;
  index?: number;
  progressBar?: "none" | "normal" | "reverse";
  variant: ToastVariant;
  action?: {
    label: string;
    onClick: () => void;
  };
  includeIcon?: boolean;
  SvgIcon?: React.FC<React.SVGProps<SVGSVGElement>>;
}

interface ToastIconProps extends React.SVGProps<SVGSVGElement> {
  variant: ToastVariant;
  includeIcon?: boolean;
  SvgIcon?: React.FC<React.SVGProps<SVGSVGElement>>;
}

export type { ToastVariant, ToastProps, ToastIconProps };
