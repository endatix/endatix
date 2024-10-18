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
    question: string,
    answer: string
}

const MatrixAnswer = ({
    question
}: MatrixAnswerProps) => {
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
            const answerValue = question.value[row.id];
            const answerText = question.columns.find(c => c.value === answerValue).title;

            if (answerText && rowText) {
                answers.push({
                    question: rowText,
                    answer: answerText
                });
            }
        });

        return answers;
    }, [question.rows, question.columns, question.value]);

    return (
        <>
            <Label
                htmlFor={question.name}
                className={cn("text-left ", (matrixAnswers.length > 0 ? "col-span-5" : "col-span-2"))}>
                {question.title}
            </Label>
            {matrixAnswers.map(answer => (
                <>
                    <Label
                        htmlFor={answer.question}
                        className="text-right col-span-2 text-sm text-muted-foreground">
                        {answer.question}
                    </Label>
                    <div key={answer.question} className="col-span-3 items-center text-sm">
                        {answer.answer}
                    </div>
                </>
            ))}
            {!matrixAnswers.length && <Minus className="h-4 w-4" />}
            {matrixAnswers.length > 0 && <Separator className="col-span-5" />}
        </>
    );
};

export default MatrixAnswer;