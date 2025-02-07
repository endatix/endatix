"use server";

import { updateSubmissionStatus } from "@/services/api";

interface ChangeSubmissionStatusParams {
  submissionId: string;
  formId: string;
  status: string;
}

export async function changeStatusUseCase({
  submissionId,
  formId,
  status,
}: ChangeSubmissionStatusParams): Promise<boolean> {
  try {
    await updateSubmissionStatus(formId, submissionId, status);
    return true;
  } catch (error) {
    console.error("Failed to update submission status:", error);
    return false;
  }
}
