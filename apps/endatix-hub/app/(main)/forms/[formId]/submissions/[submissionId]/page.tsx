import PageTitle from "@/components/headings/page-title";
import { Button, ButtonProps } from "@/components/ui/button";
import AnswerViewer from "@/features/submissions/ui/answers/answer-viewer";
import { SubmissionActionsDropdown } from "@/features/submissions/ui/details/submission-actions-dropdown";
import { getSubmissionDetailsUseCase } from "@/features/submissions/use-cases/get-submission-details.use-case";
import { Result } from "@/lib/result";
import { cn } from "@/lib/utils";
import { ArrowLeftIcon, Download, FilePenLine, LinkIcon, Sparkles, Trash2 } from "lucide-react";
import Link from "next/link";
import { Model } from "survey-core";

interface SubmissionPageProps {
    params: {
        formId: string;
        submissionId: string;
    };
}

export default async function SubmissionPage({ params }: SubmissionPageProps) {
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
    const questions = surveyModel.getAllQuestions(true, true, true);

    const getFormattedDate = (date?: Date) => {
        if (!date) {
            return;
        }

        return new Date(date).toLocaleString("en-US", {
            hour: "2-digit",
            minute: "2-digit",
            month: "2-digit",
            day: "2-digit",
            year: "numeric",
            hour12: true,
        });
    };

    return (
        <>
            <div className="sticky top-0 z-50 w-full border-b border-border/40 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 dark:border-border">
                <BackToSubmissionsButton
                    formId={formId}
                    text="Back to submissions"
                    variant="link"
                />
            </div>
            <div className="px-4">
                <div className="my-2 flex flex-col gap-6 md:gap-2 md:flex-row  md:justify-between">
                    <PageTitle title="Submission Details" />
                    <div className="flex space-x-2 justify-end text-muted-foreground">
                        <Link href="#">
                            <Button variant={"outline"}>
                                <Download className="mr-2 h-4 w-4" />
                                Export PDF
                            </Button>
                        </Link>
                        <Link href="#">
                            <Button variant={"outline"}>
                                <LinkIcon className="mr-2 h-4 w-4" />
                                Share Link
                            </Button>
                        </Link>
                        <Link href="#" className="md:block hidden">
                            <Button variant={"outline"}>
                                <FilePenLine className="mr-2 h-4 w-4" />
                                Edit
                            </Button>
                        </Link>
                        <Link href="#" className="hidden md:block">
                            <Button variant={"outline"}>
                                <Sparkles className="mr-2 h-4 w-4" />
                                Mark as new
                            </Button>
                        </Link>
                        <Link href="#" className="hidden md:block">
                            <Button variant={"outline"}>
                                <Trash2 className="mr-2 h-4 w-4" />
                                Delete
                            </Button>
                        </Link>
                        <SubmissionActionsDropdown
                            submission={submission}
                            className="text-muted-foreground md:hidden block"
                        />
                    </div>
                </div>

                {submission.isComplete ? (
                    <div className="grid grid-cols-5 py-2 items-center gap-4">
                        <span className="text-right self-start col-span-2">
                            Submitted on
                        </span>
                        <span className="text-sm text-muted-foreground col-span-3">
                            {getFormattedDate(submission.completedAt)}
                        </span>
                    </div>
                ) : (
                    <div className="grid grid-cols-5 py-2 items-center gap-4">
                        <span className="text-right self-start col-span-2">
                            Last updated on
                        </span>
                        <span className="text-sm text-muted-foreground col-span-3">
                            {getFormattedDate(submission.createdAt)}
                        </span>
                    </div>
                )}
                <div className="grid gap-4 py-4">
                    {questions?.map((question) => {
                        return (
                            <div key={question.id}
                                className="grid grid-cols-5 items-center gap-4">
                                <AnswerViewer key={question.id} forQuestion={question} />
                            </div>
                        );
                    })}
                </div>
                <div className="grid grid-cols-5 py-2 items-center gap-4">
                    <span className="text-right self-start col-span-2">
                        Is Complete
                    </span>
                    <span className="text-sm text-muted-foreground col-span-3">
                        <span
                            className={cn(
                                "flex h-2 w-2 mr-1 rounded-full inline-block",
                                submission.isComplete ? "bg-green-600" : "bg-gray-600"
                            )}
                        />
                        {submission.isComplete ? "Yes" : "No"}
                    </span>
                </div>
            </div>
        </>
    )
}

interface BackToSubmissionsButtonProps extends ButtonProps {
    formId: string;
    text?: string;
}

function BackToSubmissionsButton({
    formId,
    text = "Back to submissions",
    variant,
    ...props
}: BackToSubmissionsButtonProps) {
    return (
        <Button variant={variant} asChild {...props}>
            <Link href={`/forms/${formId}/submissions`} className="flex items-center gap-2">
                <ArrowLeftIcon className="w-4 h-4" />
                {text}
            </Link>
        </Button>
    )
}