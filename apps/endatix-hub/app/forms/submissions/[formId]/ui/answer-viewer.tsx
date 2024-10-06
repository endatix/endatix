import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import React from "react";
import { Question } from "survey-core";
import RatingAnswer from "./rating-answer";
import RadioGroupAnswer from "./radiogroup-answer";
import DropdownAnswer from "./dropdown-answer";
import RankingAnswer from "./ranking-answer";
import { Label } from "@/components/ui/label";
import MatrixAnswer from "./matrix-answer";

export interface ViewAnswerProps
    extends React.HtmlHTMLAttributes<HTMLInputElement> {
    forQuestion: Question;
}

export enum QuestionType {
    Text = "text",
    Boolean = "boolean",
    Rating = "rating",
    Radiogroup = "radiogroup",
    Dropdown = "dropdown",
    Ranking = "ranking",
    Matrix = "matrix",
    Unsupported = "unsupported",
}

const AnswerViewer = ({ forQuestion }: ViewAnswerProps) => {
    const questionType = forQuestion.getType() ?? "unsupported";

    if (questionType === QuestionType.Matrix) {
        debugger;
    }

    const renderTextAnswer = () => (
        <>
            <Label htmlFor={forQuestion.name}
                className="text-right col-span-2">
                {forQuestion.title}
            </Label>
            <Input
                disabled
                id={forQuestion.name}
                value={forQuestion.value}
                className="col-span-3 bg-accent"
            />
        </>
    );

    const renderCheckboxAnswer = () => (
        <>
            <Label
                htmlFor={forQuestion.name}
                className="text-right col-span-2">
                {forQuestion.title}
            </Label>
            <Checkbox
                disabled
                checked={forQuestion.value}
                className="col-span-3 self-start"
            />
        </>
    );

    const renderRatingAnswer = () => (
        <>
            <Label
                htmlFor={forQuestion.name}
                className="text-right col-span-2">
                {forQuestion.title}
            </Label>
            <RatingAnswer
                question={forQuestion}
                className="col-span-3 self-start"
            />
        </>
    );

    const renderRadiogroupAnswer = () => (
        <>
            <Label
                htmlFor={forQuestion.name}
                className="text-right col-span-2">
                {forQuestion.title}
            </Label>
            <RadioGroupAnswer
                question={forQuestion}
                className="col-span-3 self-start"
            />
        </>
    );

    const renderDropdownAnswer = () => (
        <>
            <Label
                htmlFor={forQuestion.name}
                className="text-right col-span-2">
                {forQuestion.title}
            </Label>
            <DropdownAnswer
                question={forQuestion}
                className="col-span-3 self-start"
            />
        </>
    );

    const renderRankingAnswer = () => (
        <>
            <Label
                htmlFor={forQuestion.name}
                className="text-right col-span-2">
                {forQuestion.title}
            </Label>
            <RankingAnswer question={forQuestion} className="col-span-3 self-start" />
        </>
    )

    const renderMatrixAnswer = () => (
       <MatrixAnswer question={forQuestion} />
    );

    switch (questionType) {
        case QuestionType.Text:
            return renderTextAnswer();
        case QuestionType.Boolean:
            return renderCheckboxAnswer();
        case QuestionType.Rating:
            return renderRatingAnswer();
        case QuestionType.Radiogroup:
            return renderRadiogroupAnswer();
        case QuestionType.Dropdown:
            return renderDropdownAnswer();
        case QuestionType.Ranking:
            return renderRankingAnswer();
        case QuestionType.Matrix:
            return renderMatrixAnswer();
        default:
            return <p className="col-span-3">Unsupported question type</p>;
    }
};

export default AnswerViewer;
