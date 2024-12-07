"use server";

import { cookies } from 'next/headers';
import { createSubmission, updateExistingSubmission } from '@/services/api';
import { Result } from '@/lib/result';
import { ReadonlyRequestCookies } from 'next/dist/server/web/spec-extension/adapters/request-cookies';
import { deleteTokenFromCookie, getTokenFromCookie, setTokenInCookie, TOKENS_COOKIE_OPTIONS } from './lib/cookie-store';

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
    const partialSubmissionKeysCookie = cookieStore.get(TOKENS_COOKIE_OPTIONS.name);
    const tokenResult = getTokenFromCookie(partialSubmissionKeysCookie, formId);

    // If we have a valid token, update the existing submission and update the cookie if the submission fails or is complete
    if (Result.isSuccess(tokenResult)) {
        return await updateExistingSubmissionViaToken(formId, tokenResult.value, submissionData, cookieStore);
    }

    // Otherwise create a new submission and update the cookie with the new token
    return await createNewSubmission(formId, submissionData, cookieStore);
}

async function updateExistingSubmissionViaToken(
    formId: string,
    token: string,
    submissionData: SubmissionData,
    cookieStore: ReadonlyRequestCookies
): Promise<Result<SubmissionOperation>> {
    try {
        const updatedSubmission = await updateExistingSubmission(formId, token, submissionData);
        if (updatedSubmission.isComplete) {
            cookieStore.delete(TOKENS_COOKIE_OPTIONS.name);
        }
        return Result.success({ isSuccess: true });
    } catch (err) {
        cookieStore.delete(TOKENS_COOKIE_OPTIONS.name);
        return Result.error('Failed to update existing submission. Details: ' + err);
    }
}

async function createNewSubmission(
    formId: string,
    submissionData: SubmissionData,
    cookieStore: ReadonlyRequestCookies
): Promise<Result<SubmissionOperation>> {
    try {
        const createSubmissionResponse = await createSubmission(formId, submissionData);
        if (createSubmissionResponse.isComplete) {
            deleteTokenFromCookie(cookieStore, formId);
        } else {
            setTokenInCookie(cookieStore, formId, createSubmissionResponse.token);
        }

        return Result.success({ isSuccess: true });
    } catch (err) {
        return Result.error('Failed to create new submission');
    }
}