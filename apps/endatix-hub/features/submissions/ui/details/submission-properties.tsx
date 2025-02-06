import { getElapsedTimeString, parseDate } from '@/lib/utils';
import { CellCompleteStatus } from '../table/cell-complete-status';
import { PropertyDisplay } from './property-display';
import { Submission, SubmissionStatus, SubmissionStatusKind } from '@/types';
import { Badge } from '@/components/ui/badge';

interface SubmissionPropertiesProps {
  submission: Submission;
}

const getFormattedDate = (date: Date): string => {
  const parsedDate = parseDate(date);
  if (!parsedDate) {
    return '-';
  }

  return parsedDate.toLocaleString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
    month: 'short',
    day: '2-digit',
    year: 'numeric',
    hour12: true,
  });
};

export function SubmissionProperties({
  submission,
}: SubmissionPropertiesProps) {
  const status = SubmissionStatus.fromCode(submission.status);

  return (
    <div className="px-4">
      <PropertyDisplay label="Created on">
        {getFormattedDate(submission.createdAt)}
      </PropertyDisplay>
      <PropertyDisplay label="Is Complete?" valueClassName="uppercase">
        <CellCompleteStatus isComplete={submission.isComplete} />
      </PropertyDisplay>
      <PropertyDisplay label="Completed on">
        {getFormattedDate(submission.completedAt)}
      </PropertyDisplay>
      {submission.isComplete && (
        <PropertyDisplay label="Completion time">
          {getElapsedTimeString(
            submission.createdAt,
            submission.completedAt,
            'long'
          )}
        </PropertyDisplay>
      )}
      <PropertyDisplay label="Status">
        <Badge variant={status.value === SubmissionStatusKind.New ? 'default' : 'secondary'}>
          {status.label}
        </Badge>
      </PropertyDisplay>
      <PropertyDisplay label="Last modified on">
        {getFormattedDate(submission.modifiedAt)}
      </PropertyDisplay>
    </div>
  );
}
