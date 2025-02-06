import { SubmissionStatusKind } from "@/types";

export interface ChangeStatusCommand {
  submissionId: string;
  formId: string;
  status: SubmissionStatusKind;
}

export interface ChangeStatusResult {
  success: boolean;
  error?: string;
}
