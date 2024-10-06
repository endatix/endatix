import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import React from "react";
import { Question } from "survey-core";
import RatingAnswer from "./rating-answer";
import RadioGroupAnswer from "./radiogroup-answer";
import DropdownAnswer from "./dropdown-answer";

export interface ViewAnswerProps
  extends React.HtmlHTMLAttributes<HTMLInputElement> {
  forQuestion: Question;
}

export enum QuestionType {
  Text = "text",
  Boolean = "boolean",
  Rating = "rating",
  Radiongroup = "radiogroup",
  Dropdown = "dropdown",
  Unsupported = "unsupported",
}

const AnswerViewer = ({ forQuestion }: ViewAnswerProps) => {
  const questionType = forQuestion.getType() ?? "unsupported";

  if (questionType === QuestionType.Dropdown) {
    debugger;
  }

  switch (questionType) {
    case QuestionType.Text:
      return (
        <Input
          disabled
          id={forQuestion.name}
          value={forQuestion.value}
          className="col-span-3 bg-accent"
        />
      );
    case QuestionType.Boolean:
      return (
        <Checkbox
          disabled
          checked={forQuestion.value}
          className="col-span-3 self-start"
        />
      );
    case QuestionType.Rating:
      return (
        <RatingAnswer
          question={forQuestion}
          className="col-span-3 self-start"
        />
      );
    case QuestionType.Radiongroup:
      return (
        <RadioGroupAnswer
          question={forQuestion}
          className="col-span-3 self-start"
        />
      );
    case QuestionType.Dropdown:
      return (
        <DropdownAnswer
          question={forQuestion}
          className="col-span-3 self-start"
        />
      );
    default:
      return <p className="col-span-3">Unsupported question type</p>;
  }
};

export default AnswerViewer;