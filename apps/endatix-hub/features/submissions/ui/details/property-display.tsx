import { cn } from "@/lib/utils";
import { ReactNode } from "react";

interface PropertyDisplayProps extends React.HTMLAttributes<HTMLDivElement> {
  label: string;
  children: ReactNode;
  valueClassName?: string;
}

export function PropertyDisplay({
  label,
  children,
  className,
  valueClassName,
  ...props
}: PropertyDisplayProps) {
  return (
    <div
      className={cn(className, "grid grid-cols-5 py-2 items-center gap-4")}
      {...props}
    >
      <span className="text-right text-muted-foreground self-start col-span-2">
        {label}
      </span>
      <span className={cn("col-span-3", valueClassName)}>{children}</span>
    </div>
  );
}
