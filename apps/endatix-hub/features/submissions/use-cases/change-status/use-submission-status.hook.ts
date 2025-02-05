'use client';

import { toast } from 'sonner';
import { useTransition } from 'react';
import { changeStatusAction } from './change-status.action';

interface UseSubmissionStatusProps {
  submissionId: string;
  formId: string;
  submissionStatus: string;
}

export const useSubmissionStatus = ({
  submissionId,
  formId,
  submissionStatus,
}: UseSubmissionStatusProps) => {
  const [isPending, startTransition] = useTransition();
  const nextStatus = submissionStatus === 'new' ? 'seen' : 'new';

  const handleStatusChange = async () => {
    startTransition(async () => {
      const statusResult = await changeStatusAction({
        submissionId,
        formId,
        status: nextStatus,
      });

      if (statusResult.success) {
        toast.success('Status updated successfully');
      } else {
        toast.error(statusResult.error);
      }
    });
  };

  return {
    isPending,
    nextStatus,
    handleStatusChange,
  };
};
