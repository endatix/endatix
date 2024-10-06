import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import React from "react";
import { Question } from "survey-core";
import RatingAnswer from "./rating-answer";
import RadioGroupAnswer from "./radiogroup-answer";
import DropdownAnswer from "./dropdown-answer";
import RankingAnswer from "./ranking-answer";

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
    Unsupported = "unsupported",
}

const AnswerViewer = ({ forQuestion }: ViewAnswerProps) => {
    const questionType = forQuestion.getType() ?? "unsupported";

    if (questionType === QuestionType.Ranking) {
        debugger;
    }

    const renderTextAnswer = () => (
        <Input
            disabled
            id={forQuestion.name}
            value={forQuestion.value}
            className="col-span-3 bg-accent"
        />
    );

    const renderCheckboxAnswer = () => (
        <Checkbox
            disabled
            checked={forQuestion.value}
            className="col-span-3 self-start"
        />
    );

    const renderRatingAnswer = () => (
        <RatingAnswer
            question={forQuestion}
            className="col-span-3 self-start"
        />
    );

    const renderRadiogroupAnswer = () => (
        <RadioGroupAnswer
            question={forQuestion}
            className="col-span-3 self-start"
        />
    );

    const renderDropdownAnswer = () => (
        <DropdownAnswer
            question={forQuestion}
            className="col-span-3 self-start"
        />
    );

    const renderRankingAnswer = () => (
        <RankingAnswer question={forQuestion} className="col-span-3 self-start" />
    )

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
        default:
            return <p className="col-span-3">Unsupported question type</p>;
    }
};

export default AnswerViewer;
