import { Spinner } from '@/components/loaders/spinner';
import { cn } from '@/lib/utils';
import { SubmissionStatus, SubmissionStatusType } from '@/types';

interface SubmissionStatusIconProps {
  className?: string;
  isPending: boolean;
  nextStatus: SubmissionStatusType;
}

const SubmissionStatusIcon = ({
  className,
  isPending,
  nextStatus,
}: SubmissionStatusIconProps) => {
  if (isPending) {
    return <Spinner className={cn('h-4 w-4', className)} />;
  }

  const Icon = SubmissionStatus.getMetadata(nextStatus).icon;

  return <Icon className={cn('h-4 w-4', className)} />;
};

export { SubmissionStatusIcon };
