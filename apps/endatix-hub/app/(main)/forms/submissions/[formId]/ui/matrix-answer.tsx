import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import { cn } from "@/lib/utils";
import { Minus } from "lucide-react";
import React from "react";
import { Question } from "survey-core";

interface MatrixAnswerProps extends React.HtmlHTMLAttributes<HTMLDivElement> {
  question: Question;
}

interface MatrixAnswer {
  question: string;
  answer: string;
}

const MatrixAnswer = ({ question }: MatrixAnswerProps) => {
  const matrixAnswers = React.useMemo(() => {
    if (!question.rows || !question.columns) {
      return [];
    }

    const answers: Array<MatrixAnswer> = [];
    question.rows.forEach((row) => {
      if (!question?.value) {
        return;
      }
      const rowText = row.text;
      const answer = question.value[row.id];
      const answerText = question.columns.find(
        (c : any) => c.value === answer
      )?.title ?? ""; 

      if (answerText && rowText) {
        answers.push({
          question: rowText,
          answer: answerText,
        });
      }
    });

    return answers;
  }, [question.rows, question.columns, question.value]);

  return (
    <>
      <Label
        htmlFor={question.name}
        className={cn(
          "text-left ",
          matrixAnswers.length > 0 ? "col-span-5" : "col-span-2"
        )}
      >
        {question.title}
      </Label>
      {matrixAnswers.map((answer) => (
        <div key={answer.question} className="grid grid-cols-5 col-span-5 gap-4">
          <Label
            htmlFor={answer.question}
            className="pl-8 text-left text-sm text-muted-foreground col-span-2"
          >
            {answer.question}
          </Label>
          <div className="items-center text-left text-sm col-span-3">{answer.answer}</div>
        </div>
      ))}
      {!matrixAnswers.length && <Minus className="h-4 w-4" />}
      {matrixAnswers.length > 0 && <Separator className="col-span-5" />}
    </>
  );
};

export default MatrixAnswer;
