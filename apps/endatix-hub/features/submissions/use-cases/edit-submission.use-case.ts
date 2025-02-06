"use server";

import { SubmissionData } from "@/features/public-form/application/actions/submit-form.action";
import { updateSubmission } from "@/services/api";

export const editSubmissionUseCase = async (
  formId: string,
  submissionId: string,
  submissionData: SubmissionData,
) => {
  const submission = await updateSubmission(
    formId,
    submissionId,
    submissionData,
  );
  return submission;
};
