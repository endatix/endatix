import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
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
    question,
    className,
    ...props
}: MatrixAnswerProps) => {
    const getMatrixAnswers = (): Array<MatrixAnswer> => {
        if (!question.rows || !question.columns) {
            return [];
        }

        const answers: Array<MatrixAnswer> = [];
        question.rows.forEach((row) => {
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
    }

    return (
        <>
            <Label
                htmlFor={question.name}
                className="text-left col-span-5">
                {question.title}
            </Label>
            {getMatrixAnswers().map(answer => (
                <>
                    <Label
                        htmlFor={answer.question}
                        className="text-right col-span-2 text-muted-foreground">
                        {answer.question}
                    </Label>
                    <div key={answer.question} className="col-span-3 items-center text-sm">
                        {answer.answer}
                    </div>
                </>
            ))}
            <Separator className="col-span-5"/>
        </>
    );
};

export default MatrixAnswer;