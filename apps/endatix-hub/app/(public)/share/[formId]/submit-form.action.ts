"use server";

import { cookies } from 'next/headers';
import { createSubmission, updateExistingSubmission } from '@/services/api';
import { Result } from '@/lib/result';
import { RequestCookie, ResponseCookie } from 'next/dist/compiled/@edge-runtime/cookies';
import { ReadonlyRequestCookies } from 'next/dist/server/web/spec-extension/adapters/request-cookies';

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

const TOKENS_COOKIE_OPTIONS = {
    name: 'FPSK',
    secure: process.env.NODE_ENV === 'production',
    expirationInDays: 7,
    getExpires: () => new Date(Date.now() + TOKENS_COOKIE_OPTIONS.expirationInDays * 24 * 60 * 60 * 1000),
    getCookieOptions: (): Partial<ResponseCookie> => ({
        httpOnly: true,
        secure: TOKENS_COOKIE_OPTIONS.secure,
        sameSite: "strict",
        expires: TOKENS_COOKIE_OPTIONS.getExpires()
    })
};

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

function getTokenFromCookie(tokensCookie: RequestCookie | undefined, formId: string): Result<string> {
    if (!tokensCookie || !formId) {
        return Result.error('No cookie or formId provided');
    }

    try {
        const partialTokens = JSON.parse(tokensCookie.value);
        const tokenForCurrentForm = partialTokens[formId];

        return tokenForCurrentForm ?
            Result.success(tokenForCurrentForm) :
            Result.error('No token found for the current form');
    } catch (error) {
        return Result.error('Error parsing FPSK cookie. Details: ' + error);
    }
}

function deleteTokenFromCookie(cookieStore: ReadonlyRequestCookies, formId: string) {
    const partialTokens = JSON.parse(cookieStore.get(TOKENS_COOKIE_OPTIONS.name)?.value || '{}');
    const { [formId]: _, ...remainingTokens } = partialTokens;

    if (Object.keys(remainingTokens).length === 0) {
        cookieStore.delete(TOKENS_COOKIE_OPTIONS.name);
    } else {
        const cookieValue = JSON.stringify(remainingTokens);
        const cookieOptions = TOKENS_COOKIE_OPTIONS.getCookieOptions();
        cookieStore.set(TOKENS_COOKIE_OPTIONS.name, cookieValue, cookieOptions);
    }
}

function setTokenInCookie(cookieStore: ReadonlyRequestCookies, formId: string, token: string) {
    const cookieValue = JSON.stringify({ [formId]: token });
    const cookieOptions = TOKENS_COOKIE_OPTIONS.getCookieOptions();
    cookieStore.set(TOKENS_COOKIE_OPTIONS.name, cookieValue, cookieOptions);
}   