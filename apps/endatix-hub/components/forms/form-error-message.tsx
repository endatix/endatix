import { cn } from '@/lib/utils';
import * as React from 'react';

export interface ErrorMessageProps extends React.HTMLAttributes<HTMLParagraphElement> {
  message: string;
}

export const FormErrorMessage = React.forwardRef<HTMLParagraphElement, ErrorMessageProps>(
  ({ className, message, ...props }, ref) => {
    if (!message) {
      return null;
    }

    return (
      <p
        ref={ref}
        className={cn('text-sm font-medium text-destructive', className)}
        {...props}
      >
        {message}
      </p>
    );
  }
);

FormErrorMessage.displayName = 'FormErrorMessage';
