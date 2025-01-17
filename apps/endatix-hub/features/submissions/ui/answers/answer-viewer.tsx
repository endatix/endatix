import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import React from "react";
import { Question } from "survey-core";
import RatingAnswer from "./rating-answer";
import RadioGroupAnswer from "./radiogroup-answer";
import DropdownAnswer from "./dropdown-answer";
import RankingAnswer from "./ranking-answer";
import MatrixAnswer from "./matrix-answer";
import CommentAnswer from "./comment-answer";
import { FileAnswer } from "./file-answer";
import { QuestionFileModelBase } from "survey-core/typings/packages/survey-core/src/question_file";
import { QuestionLabel } from "../details/question-label";

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
  Comment = "comment",
  File = "file",
  Unsupported = "unsupported",
}

const AnswerViewer = ({ forQuestion }: ViewAnswerProps): React.JSX.Element => {
  const questionType = forQuestion.getType() ?? "unsupported";

  const renderTextAnswer = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
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
      <QuestionLabel forQuestion={forQuestion} />
      <Checkbox
        disabled
        checked={forQuestion.value}
        className="col-span-3 self-start"
      />
    </>
  );

  const renderRatingAnswer = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <RatingAnswer question={forQuestion} className="col-span-3 self-start" />
    </>
  );

  const renderRadiogroupAnswer = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <RadioGroupAnswer
        question={forQuestion}
        className="col-span-3 self-start"
      />
    </>
  );

  const renderDropdownAnswer = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <DropdownAnswer
        question={forQuestion}
        className="col-span-3 self-start"
      />
    </>
  );

  const renderRankingAnswer = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <RankingAnswer question={forQuestion} className="col-span-3 self-start" />
    </>
  );

  const renderMatrixAnswer = () => <MatrixAnswer question={forQuestion} />;

  const renderCommentAnswer = () => <CommentAnswer question={forQuestion} />;

  const renderFileAnswer = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <FileAnswer
        question={forQuestion as QuestionFileModelBase}
        className="col-span-3"
      />
    </>
  );

  const renderUnknownAnswer = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <p className="col-span-3">{forQuestion.value?.toString() ?? "-"}</p>
    </>
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
    case QuestionType.Comment:
      return renderCommentAnswer();
    case QuestionType.File:
      return renderFileAnswer();
    default:
      return renderUnknownAnswer();
  }
};

export default AnswerViewer;
