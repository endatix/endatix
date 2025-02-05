import { Spinner } from '@/components/loaders/spinner';
import { Eye, Sparkles, Variable } from 'lucide-react';
import { cn } from '@/lib/utils';

interface SubmissionStatusIconProps {
  className?: string;
  isPending: boolean;
  nextStatus: string;
}

const SubmissionStatusIcon = ({
  className,
  isPending,
  nextStatus,
}: SubmissionStatusIconProps) => {
  if (isPending) {
    return <Spinner className={cn('h-4 w-4', className)} />;
  }

  switch (nextStatus) {
    case 'new':
      return <Sparkles className={cn('h-4 w-4', className)} />;
    case 'seen':
      return <Eye className={cn('h-4 w-4', className)} />;
    default:
      return <Variable className={cn('h-4 w-4', className)} />;
  }
};

export { SubmissionStatusIcon };
