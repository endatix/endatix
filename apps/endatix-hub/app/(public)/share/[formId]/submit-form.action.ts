"use server";

import { sendSubmission } from '@/services/api';

export async function submitFormAction(formId: string, submissionData: unknown) {
    const response = sendSubmission(formId, submissionData);
    return response;
}
