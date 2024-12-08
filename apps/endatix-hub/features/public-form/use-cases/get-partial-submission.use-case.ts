import { Submission } from "@/types";
import { FormTokenCookieStore } from "../infrastructure/cookie-store";
import { getSubmission } from "@/services/api";
import { Result } from "@/lib/result";

export type PartialSubmissionResult = Result<Submission>;

export type GetPartialSubmissionQuery = {
    formId: string;
    tokenStore: FormTokenCookieStore;
}

export const getPartialSubmissionUseCase = async (
    { formId, tokenStore }: GetPartialSubmissionQuery): Promise<PartialSubmissionResult> => {
    if (!formId) {
        return Result.error("Form ID is required");
    }

    if (!tokenStore) {
        return Result.error("Token store is required");
    }

    const tokenResult = tokenStore.getToken(formId);
    if (Result.isError(tokenResult)) {
        return Result.error(tokenResult.message);
    }

    const token = tokenResult.value;

    try {
        const submission = await getSubmission(formId, token);
        return Result.success(submission);
    } catch (error) {
        const errorMessage = `Failed to load submission: ${error instanceof Error ? error.message : "Unknown error"}`;
        tokenStore.deleteToken(formId);
        console.error(errorMessage);

        return Result.error(errorMessage);
    }
}
