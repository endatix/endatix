'use client';

import { DropdownMenuItem } from '@/components/ui/dropdown-menu';
import { useSubmissionStatus } from './use-submission-status.hook';
import React from 'react';
import { DropdownMenuItemProps } from '@radix-ui/react-dropdown-menu';
import { SubmissionStatusIcon } from './submission-status-icon';
import { SubmissionStatus, SubmissionStatusType } from '@/types';

interface SubmissionStatusMenuItemProps extends DropdownMenuItemProps {
  submissionId: string;
  formId: string;
  status: SubmissionStatusType;
}

const SubmissionStatusMenuItem = React.forwardRef<
  HTMLDivElement,
  SubmissionStatusMenuItemProps
>(({ submissionId, formId, status, onClick, ...props }, ref) => {
  const { isPending, nextStatus, handleStatusChange } = useSubmissionStatus({
    submissionId,
    formId,
    status,
  });

  return (
    <DropdownMenuItem
      ref={ref}
      className="cursor-pointer"
      disabled={isPending}
      onClick={(e) => {
        handleStatusChange();
        onClick?.(e);
      }}
      {...props}
    >
      <SubmissionStatusIcon
        isPending={isPending}
        nextStatus={nextStatus}
        className='mr-2'
      />
      {nextStatus === SubmissionStatus.values.new
        ? 'Mark as New'
        : 'Mark as Seen'}
    </DropdownMenuItem>
  );
});

SubmissionStatusMenuItem.displayName = 'SubmissionStatusMenuItem';

export { SubmissionStatusMenuItem };
