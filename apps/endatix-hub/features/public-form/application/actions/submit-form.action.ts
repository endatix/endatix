'use server'

import { cookies } from 'next/headers';
import { createSubmission, updateSubmission } from '@/services/api';
import { Result } from '@/lib/result';
import { FormTokenCookieStore } from '../../../../features/public-form/infrastructure/cookie-store';

export type SubmissionData = {
    isComplete?: boolean;
    jsonData: string,
    currentPage?: number;
    metadata?: string;
}

export type SubmissionOperation = {
    isSuccess: boolean;
}

export type SubmissionOperationResult = Result<SubmissionOperation>;

/**
 * Handles form submission by either updating an existing submission or creating a new one.
 * Uses Next.js server side processing of cookies to securely track partial submissions across requests.
 * 
 * @param formId - The unique identifier of the form being submitted
 * @param submissionData - The data being submitted, including form responses and completion status
 * @returns A Result indicating success or failure of the submission operation
 */
export async function submitFormAction(formId: string, submissionData: SubmissionData): Promise<SubmissionOperationResult> {
    // Get cookie store and check for existing submission token
    const cookieStore = await cookies();
    var tokenStore = new FormTokenCookieStore(cookieStore);
    const tokenResult = tokenStore.getToken(formId);

    // If we have a valid token, update the existing submission and update the cookie if the submission fails or is complete
    if (Result.isSuccess(tokenResult)) {
        return await updateExistingSubmissionViaToken(formId, tokenResult.value, submissionData, tokenStore);
    }

    // Otherwise create a new submission and update the cookie with the new token
    return await createNewSubmission(formId, submissionData, tokenStore);
}

async function updateExistingSubmissionViaToken(
    formId: string,
    token: string,
    submissionData: SubmissionData,
    tokenStore: FormTokenCookieStore
): Promise<Result<SubmissionOperation>> {
    try {
        const updatedSubmission = await updateSubmission(formId, token, submissionData);
        if (updatedSubmission.isComplete) {
            tokenStore.deleteToken(formId);
        }
        return Result.success({ isSuccess: true });
    } catch (err) {
        tokenStore.deleteToken(formId);
        return Result.error('Failed to update existing submission. Details: ' + err);
    }
}

async function createNewSubmission(
    formId: string,
    submissionData: SubmissionData,
    tokenStore: FormTokenCookieStore
): Promise<Result<SubmissionOperation>> {
    try {
        const createSubmissionResponse = await createSubmission(formId, submissionData);
        if (createSubmissionResponse.isComplete) {
            tokenStore.deleteToken(formId);
        } else {
            tokenStore.setToken({ formId, token: createSubmissionResponse.token });
        }

        return Result.success({ isSuccess: true });
    } catch (err) {
        return Result.error('Failed to create new submission');
    }
}