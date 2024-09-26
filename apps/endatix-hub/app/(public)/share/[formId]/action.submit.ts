"use server";

import { SubmitForm } from '@/services/api';

export async function submitForm(formId: string, submissionData: any) {
    const response = SubmitForm(formId, submissionData);
}