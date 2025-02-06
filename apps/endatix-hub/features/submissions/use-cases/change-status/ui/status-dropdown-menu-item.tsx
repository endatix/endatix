"use client";

import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { useSubmissionStatus } from "../use-submission-status.hook";
import React from "react";
import { DropdownMenuItemProps } from "@radix-ui/react-dropdown-menu";
import { SubmissionStatusIcon } from "./status-icon";

interface StatusDropdownMenuItemProps extends DropdownMenuItemProps {
  submissionId: string;
  formId: string;
  status: string;
}

const StatusDropdownMenuItem = React.forwardRef<
  HTMLDivElement,
  StatusDropdownMenuItemProps
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
        className="mr-2"
      />
      {nextStatus.isNew() ? "Mark as New" : "Mark as Read"}
    </DropdownMenuItem>
  );
});

StatusDropdownMenuItem.displayName = "StatusDropdownMenuItem";

export { StatusDropdownMenuItem };
