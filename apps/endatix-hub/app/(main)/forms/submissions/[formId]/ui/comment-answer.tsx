import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Minus } from "lucide-react";
import React from "react";
import { Question } from "survey-core";

interface CommentAnswerProps extends React.HtmlHTMLAttributes<HTMLDivElement> {
    question: Question;
}

const CommentAnswer = ({
    question
}: CommentAnswerProps) => {
    return (
        <>
            <Label
                htmlFor={question.name}
                className="text-left col-span-2">
                {question.placeholder}
            </Label>
            {question.value ? (
                <Textarea disabled className="col-span-3 bg-accent" value={question.value} />
            ) : (
                <Minus className="h-4 w-4" />
            )}
        </>
    );
};

export default CommentAnswer;