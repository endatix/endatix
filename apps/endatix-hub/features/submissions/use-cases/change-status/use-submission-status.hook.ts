"use client";

import { toast } from "sonner";
import { useTransition } from "react";
import { changeStatusAction } from "./change-status.action";
import { SubmissionStatus, SubmissionStatusKind } from "@/types";

interface UseSubmissionStatusProps {
  submissionId: string;
  formId: string;
  status: string;
}

export const useSubmissionStatus = ({
  submissionId,
  formId,
  status,
}: UseSubmissionStatusProps) => {
  const [isPending, startTransition] = useTransition();
  const currentStatus = SubmissionStatus.fromCode(status);
  const nextStatus = currentStatus.isNew()
    ? SubmissionStatus.fromCode(SubmissionStatusKind.Read)
    : SubmissionStatus.fromCode(SubmissionStatusKind.New);

  const handleStatusChange = async () => {
    startTransition(async () => {
      const statusResult = await changeStatusAction({
        submissionId,
        formId,
        status: nextStatus.value,
      });

      if (statusResult.success) {
        toast.success("Status updated successfully");
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
