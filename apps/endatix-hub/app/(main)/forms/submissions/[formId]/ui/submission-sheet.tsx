'use client';

import { Button } from "@/components/ui/button";
import { useParams } from "next/navigation";
import {
    Sheet,
    SheetContent,
    SheetFooter,
    SheetHeader,
    SheetTitle,
} from "@/components/ui/sheet";
import { Submission } from "@/types";
import { startTransition, useEffect, useState } from "react";
import {
    getDefinition,
    GetDefinitionRequest,
    SelectedDefinitionResult,
} from "../get-definition.action";
import { Model, Question } from "survey-core";
import { Download, Link as Link2, Trash } from "lucide-react";
import { toast } from "sonner";
import { comingSoonMessage } from "@/components/layout-ui/teasers/coming-soon-link";
import Link from "next/link";
import { cn } from "@/lib/utils";
import AnswerViewer from "./answer-viewer";

type SubmissionSheetProps = {
    submission: Submission | null;
};

const SubmissionSheet = ({ submission }: SubmissionSheetProps) => {
    const params = useParams<{ formId: string }>();
    const [surveyModel, setSurveyModel] = useState<Model>();
    const [questions, setQuestions] = useState<Question[]>();

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

    useEffect(() => {
        const changeSelectedSubmission = async () => {
            startTransition(async () => {
                const getDefinitionRequest: GetDefinitionRequest = {
                    formId: params.formId,
                    definitionId: submission?.formDefinitionId,
                };
                const result: SelectedDefinitionResult = await getDefinition(
                    getDefinitionRequest
                );
                if (result.isSuccess && result.definitionsData && submission) {
                    const json = JSON.parse(result.definitionsData);
                    const survey = new Model(json);

                    let submissionData = {};
                    try {
                        submissionData = JSON.parse(submission?.jsonData);
                    } catch (ex) {
                        console.warn("Error while parsing submission's JSON data", ex);
                    }

                    survey.data = submissionData;
                    setSurveyModel(survey);
                    setQuestions(survey.getAllQuestions(true, true, true));
                }
            });
        };

        const fetchDefinition = async () => {
            if (submission) {
                await changeSelectedSubmission();
            }
        };

        fetchDefinition();
    }, [submission, params]);

    return (
        submission &&
        surveyModel && (
            <Sheet modal={false} open={submission != null}>
                <SheetContent className="w-[720px] sm:w-[620px] sm:max-w-none overflow-auto">
                    <SheetHeader>
                        <SheetTitle>
                            {surveyModel?.title} <Link2 className="inline-block ml-4" />
                        </SheetTitle>
                    </SheetHeader>
                    <div className="my-8 flex space-x-2">
                        <Link href="#" onClick={() => toast(comingSoonMessage)}>
                            <Button variant={"outline"}>
                                <Download className="mr-2 h-4 w-4" />
                                Export PDF
                            </Button>
                        </Link>
                        <Link href="#" onClick={() => toast(comingSoonMessage)}>
                            <Button variant={"outline"}>
                                <Trash className="mr-2 h-4 w-4" />
                                Delete
                            </Button>
                        </Link>
                        <Link href="#" onClick={() => toast(comingSoonMessage)}>
                            <Button variant={"outline"}>Mark as spam</Button>
                        </Link>
                    </div>
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
                    <SheetFooter></SheetFooter>
                </SheetContent>
            </Sheet>
        )
    );
};

export default SubmissionSheet;
