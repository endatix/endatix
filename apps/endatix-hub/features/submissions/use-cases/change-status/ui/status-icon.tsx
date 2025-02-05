import { Spinner } from '@/components/loaders/spinner';
import { cn } from '@/lib/utils';
import { SubmissionStatus, SubmissionStatusType } from '@/types';

interface StatusIconProps {
  className?: string;
  isPending: boolean;
  nextStatus: SubmissionStatusType;
}

const StatusIcon = ({
  className,
  isPending,
  nextStatus,
}: StatusIconProps) => {
  if (isPending) {
    return <Spinner className={cn('h-4 w-4', className)} />;
  }

  const Icon = SubmissionStatus.getMetadata(nextStatus).icon;

  return <Icon className={cn('h-4 w-4', className)} />;
};

export { StatusIcon as SubmissionStatusIcon };
