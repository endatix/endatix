import { Submission } from "@/types";
import { getSubmission } from "@/services/api";
import { Result } from "@/lib/result";

export type GetSubmissionDetailsQuery = {
    formId: string;
    submissionId: string;
}

export type SubmissionDetailsResult = Result<Submission>;

export const getSubmissionDetailsUseCase = async ({
    formId,
    submissionId
}: GetSubmissionDetailsQuery): Promise<SubmissionDetailsResult> => {
    try {
        const response: Submission = await getSubmission(formId, submissionId);
        return Result.success(response);
    } catch (error) {
        const errorMessage = `Failed to load submission details: ${error instanceof Error ? error.message : 'Unknown error'}`;
        console.error(errorMessage);

        return Result.error(errorMessage);
    }
}
