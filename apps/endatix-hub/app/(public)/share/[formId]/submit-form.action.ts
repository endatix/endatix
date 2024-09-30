"use server";

import { sendSubmission } from '@/services/api';

export async function SubmitFormAction(formId: string, submissionData: any) {
    const response = sendSubmission(formId, submissionData);
}