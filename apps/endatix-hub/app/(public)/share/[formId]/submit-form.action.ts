"use server";

import { createSubmission } from '@/services/api';

export async function submitFormAction(formId: string, submissionData: unknown) {
    const response = createSubmission(formId, submissionData);
    return response;
}
