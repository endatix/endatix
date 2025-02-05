'use client';

import { Button, ButtonProps } from '@/components/ui/button';
import React from 'react';
import { useSubmissionStatus } from './use-submission-status.hook';
import { SubmissionStatusIcon } from './submission-status-icon';
import { SubmissionStatus, SubmissionStatusType } from '@/types';

interface SubmissionStatusButtonProps extends ButtonProps {
  submissionId: string;
  formId: string;
  status: SubmissionStatusType;
}

const SubmissionStatusButton = React.forwardRef<
  HTMLButtonElement,
  SubmissionStatusButtonProps
>(
  (
    { submissionId, formId, status, variant = 'outline', onClick, ...props },
    ref
  ) => {
    const { isPending, nextStatus, handleStatusChange } = useSubmissionStatus({
      submissionId,
      formId,
      status,
    });

    return (
      <Button
        ref={ref}
        disabled={isPending}
        variant={variant}
        onClick={(e) => {
          handleStatusChange();
          onClick?.(e);
        }}
        {...props}
      >
        <SubmissionStatusIcon isPending={isPending} nextStatus={nextStatus} />
        {nextStatus === SubmissionStatus.values.new
          ? 'Mark as New'
          : 'Mark as Seen'}
      </Button>
    );
  }
);

SubmissionStatusButton.displayName = 'ChangeStatusButton';

export {
  SubmissionStatusButton as ChangeStatusButton,
  type SubmissionStatusButtonProps as ChangeStatusButtonProps,
};
