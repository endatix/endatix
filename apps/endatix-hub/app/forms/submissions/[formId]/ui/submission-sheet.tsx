"use client";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useParams } from 'next/navigation';
import {
    Sheet,
    SheetClose,
    SheetContent,
    SheetFooter,
    SheetHeader,
    SheetTitle,
} from "@/components/ui/sheet";
import { Submission } from "@/types";
import { startTransition, useEffect, useState } from "react";
import { getDefinition, GetDefinitionRequest, SelectedDefinitionResult } from "../get-definition.action";
import { Model, Question } from 'survey-core';
import { Download, Link as Link2, Trash } from "lucide-react";
import { toast } from "sonner";
import { comingSoonMessage } from "@/components/layout-ui/teasers/coming-soon-link";
import Link from "next/link";

type SubmissionSheetProps = {
    submission: Submission | null
};

const SubmissionSheet = ({ submission }: SubmissionSheetProps) => {
    const params = useParams<{ formId: string }>();
    const [surveyModel, setSurveyModel] = useState<Model>();
    const [questions, setQuestions] = useState<Question[]>();

    const changeSelectedSubmission = async () => {
        startTransition(async () => {
            const getDefinitionRequest: GetDefinitionRequest = {
                formId: params.formId,
                definitionId: submission?.formDefinitionId
            }
            const result: SelectedDefinitionResult = await getDefinition(getDefinitionRequest);
            if (result.isSuccess && result.definitionsData && submission) {
                const json = JSON.parse(result.definitionsData);
                const survey = new Model(json);
                const submissionData = JSON.parse(submission?.jsonData);
                survey.data = submissionData;
                setSurveyModel(survey);
                setQuestions(survey.getAllQuestions(false, true, true));
            }
        })
    }

    useEffect(() => {
        const fetchDefinition = async () => {
            if (submission) {
                await changeSelectedSubmission();
            }
        };

        fetchDefinition();
    }, [submission]);

    return (
        submission && surveyModel && (
            <Sheet modal={false} open={submission != null}>
                <SheetContent className="w-[720px] sm:w-[620px] sm:max-w-none">
                    <SheetHeader>
                        <SheetTitle>{surveyModel?.title} <Link2 className="inline-block" /></SheetTitle>

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
                            <Button variant={"outline"}>
                                Mark as spam
                            </Button>
                        </Link>
                    </div>
                    <div className="grid gap-4 py-4">
                        {questions?.map(question => (
                            <div className="grid grid-cols-3 items-center gap-4">
                                <Label htmlFor={question.name} className="text-right col-span-1">
                                    {question.title}
                                </Label>
                                <Input disabled id={question.name} value={question.value} className="col-span-2" />
                            </div>
                        ))}
                    </div>
                    <SheetFooter>
                        <SheetClose asChild>
                            <Button type="submit">Save changes</Button>
                        </SheetClose>
                    </SheetFooter>
                </SheetContent>
            </Sheet>
        ));
}

export default SubmissionSheet;

