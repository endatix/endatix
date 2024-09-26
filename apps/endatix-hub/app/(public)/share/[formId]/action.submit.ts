"use server";

import { sendSubmission } from '@/services/api';

export async function submitForm(formId: string, submissionData: any) {
    const response = sendSubmission(formId, submissionData);
}