"use client";

import { Button, ButtonProps } from "@/components/ui/button";
import React from "react";
import { useSubmissionStatus } from "../use-submission-status.hook";
import { SubmissionStatusIcon } from "./status-icon";

interface StatusButtonProps extends ButtonProps {
  submissionId: string;
  formId: string;
  status: string;
}

const StatusButton = React.forwardRef<HTMLButtonElement, StatusButtonProps>(
  (
    { submissionId, formId, status, variant = "outline", onClick, ...props },
    ref,
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
        {nextStatus.isNew() ? "Mark as New" : "Mark as Read"}
      </Button>
    );
  },
);

StatusButton.displayName = "StatusButton";

export { StatusButton };
