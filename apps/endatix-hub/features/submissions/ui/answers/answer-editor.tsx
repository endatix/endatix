"use client";

import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import React from 'react';
import { Question, QuestionFileModel, SurveyModel } from 'survey-core';
import RatingAnswer from './rating-answer';
import RadioGroupAnswer from './radiogroup-answer';
import DropdownAnswer from './dropdown-answer';
import RankingAnswer from './ranking-answer';
import MatrixAnswer from './matrix-answer';
import CommentAnswer from './comment-answer';
import { FileAnswer } from './file-answer';
import { QuestionLabel } from '../details/question-label';
import { QuestionType } from '@/lib/questions';

export interface EditAnswerProps
  extends React.HtmlHTMLAttributes<HTMLInputElement> {
  forQuestion: Question;
}

const AnswerEditor = ({ forQuestion }: EditAnswerProps): React.JSX.Element => {
  const questionType = forQuestion.getType() ?? 'unsupported';
  const survey = forQuestion.survey as SurveyModel;

  const renderTextAnswerEditor = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <Input
        id={forQuestion.name}
        value={forQuestion.value}
        onChange={(e) => survey.setValue(forQuestion.name, e.target.value)}
        className="col-span-3 bg-accent"
      />
    </>
  );

  const renderCheckboxAnswerEditor = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <Checkbox
        checked={forQuestion.value}
        className="col-span-3 self-start"
      />
    </>
  );

  const renderRatingAnswerEditor = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <RatingAnswer question={forQuestion} className="col-span-3 self-start" />
    </>
  );

  const renderRadiogroupAnswerEditor = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <RadioGroupAnswer
        question={forQuestion}
        className="col-span-3 self-start"
      />
    </>
  );

  const renderDropdownAnswerEditor = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <DropdownAnswer
        question={forQuestion}
        className="col-span-3 self-start"
      />
    </>
  );

  const renderRankingAnswerEditor = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <RankingAnswer question={forQuestion} className="col-span-3 self-start" />
    </>
  );

  const renderMatrixAnswerEditor = () => <MatrixAnswer question={forQuestion} />;

  const renderCommentAnswerEditor = () => <CommentAnswer question={forQuestion} />;

  const renderFileAnswerEditor = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <FileAnswer
        question={forQuestion as QuestionFileModel}
        className="col-span-3"
      />
    </>
  );

  const renderUnknownAnswerEditor = () => (
    <>
      <QuestionLabel forQuestion={forQuestion} />
      <p className="col-span-3">{forQuestion.value?.toString() ?? '-'}</p>
    </>
  );

  switch (questionType) {
    case QuestionType.Text:
      return renderTextAnswerEditor();
    case QuestionType.Boolean:
      return renderCheckboxAnswerEditor();
    case QuestionType.Rating:
      return renderRatingAnswerEditor();
    case QuestionType.Radiogroup:
      return renderRadiogroupAnswerEditor();
    case QuestionType.Dropdown:
      return renderDropdownAnswerEditor();
    case QuestionType.Ranking:
      return renderRankingAnswerEditor();
    case QuestionType.Matrix:
      return renderMatrixAnswerEditor();
    case QuestionType.Comment:
      return renderCommentAnswerEditor();
    case QuestionType.File:
    case QuestionType.Video:
      return renderFileAnswerEditor();
    default:
      return renderUnknownAnswerEditor();
  }
};

export default AnswerEditor;
