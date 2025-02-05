'use client';

import { toast } from 'sonner';
import { useTransition } from 'react';
import { changeStatusAction } from './change-status.action';
import { SubmissionStatus, SubmissionStatusType } from '@/types';

interface UseSubmissionStatusProps {
  submissionId: string;
  formId: string;
  status: SubmissionStatusType;
}

export const useSubmissionStatus = ({
  submissionId,
  formId,
  status,
}: UseSubmissionStatusProps) => {
  const [isPending, startTransition] = useTransition();
  const nextStatus =
    status === SubmissionStatus.values.new
      ? SubmissionStatus.values.seen
      : SubmissionStatus.values.new;

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
