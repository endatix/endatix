import { ButtonProps } from '../button';

type ToastVariant = "success" | "info" | "warning" | "error";

type ProgressVariant = "none" | "left-to-right" | "right-to-left";

interface ToastProps {
  id: string | number;
  variant: ToastVariant;
  title: string;
  progressBar?: ProgressVariant;
  description?: string;
  duration?: number;
  index?: number;
  action?: Omit<ButtonProps, "onClick" | "label"> & {
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

export type { ToastVariant, ToastProps, ToastIconProps, ProgressVariant };
