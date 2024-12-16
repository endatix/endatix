import AnswerViewer from "@/features/submissions/ui/answers/answer-viewer";
import { SubmissionHeader } from "@/features/submissions/ui/details/submission-header";
import { SubmissionProperties } from "@/features/submissions/ui/details/submission-properties";
import { SubmissionTopNav } from "@/features/submissions/ui/details/submission-top-nav";
import { getSubmissionDetailsUseCase } from "@/features/submissions/use-cases/get-submission-details.use-case";
import { Result } from "@/lib/result";
import { Model } from "survey-core";
import { SectionTitle } from "@/components/headings/section-title";
import { BackToSubmissionsButton } from "@/features/submissions/ui/details/back-to-submissions-button";

type Params = {
    params: Promise<{
        formId: string;
        submissionId: string;
    }>
}

export default async function SubmissionPage({ params }: Params) {
    const { formId, submissionId } = await params;
    const submissionResult = await getSubmissionDetailsUseCase({ formId, submissionId });

    if (Result.isError(submissionResult)) {
        return (
            <div>
                <h1>Submission not found</h1>
                <BackToSubmissionsButton
                    formId={formId}
                    text="All form submissions"
                    variant="default"
                />
            </div>
        )
    }

    const submission = submissionResult.value;
    if (!submission.formDefinition) {
        return <div>Form definition not found</div>;
    }
    const json = JSON.parse(submission.formDefinition.jsonData);
    const surveyModel = new Model(json);

    let submissionData = {};
    try {
        submissionData = JSON.parse(submission?.jsonData);
    } catch (ex) {
        console.warn("Error while parsing submission's JSON data", ex);
    }

    surveyModel.data = submissionData;
    const questions = surveyModel.getAllQuestions(false, false, true);

    return (
        <>
            <SubmissionTopNav formId={formId} />
            <SubmissionHeader submission={submission} />
            <SubmissionProperties submission={submission} />
            <SectionTitle
                title="Submission Answers"
                headingClassName="py-2 my-0"
            />
            <div className="grid gap-4">
                {questions?.map((question) => {
                    return (
                        <div key={question.id}
                            className="grid grid-cols-5 items-center gap-4">
                            <AnswerViewer key={question.id} forQuestion={question} />
                        </div>
                    );
                })}
            </div>
        </>
    )
}