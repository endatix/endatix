'use server';

import { revalidatePath } from 'next/cache';
import { changeStatusUseCase } from './change-status.use-case';

interface ChangeSubmissionStatusActionParams {
  submissionId: string;
  formId: string;
  status: string;
}

interface ChangeSubmissionStatusActionResult {
  success: boolean;
  error?: string;
}

export async function changeStatusAction({
  submissionId,
  formId,
  status,
}: ChangeSubmissionStatusActionParams): Promise<ChangeSubmissionStatusActionResult> {
  const success = await changeStatusUseCase({
    submissionId,
    formId,
    status,
  });

  if (success) {
    revalidatePath(`/forms/${formId}/submissions/${submissionId}`);
    return { success: true };
  }

  return {
    success: false,
    error: 'Failed to update submission status. Please try again.'
  };
}
