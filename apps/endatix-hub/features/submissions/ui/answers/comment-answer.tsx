import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Minus } from "lucide-react";
import React from "react";
import { Question } from "survey-core";
import { QuestionLabel } from "../details/question-label";

interface CommentAnswerProps extends React.HtmlHTMLAttributes<HTMLDivElement> {
  question: Question;
}

const CommentAnswer = ({ question }: CommentAnswerProps) => {
  return (
    <>
      <QuestionLabel forQuestion={question as Question} />
      {question.value ? (
        <Textarea
          disabled
          className="col-span-3 bg-accent"
          value={question.value}
        />
      ) : (
        <Minus className="h-4 w-4" />
      )}
    </>
  );
};

export default CommentAnswer;
