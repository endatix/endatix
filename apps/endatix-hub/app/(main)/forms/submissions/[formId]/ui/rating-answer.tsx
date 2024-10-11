import { cn } from "@/lib/utils";
import { Minus, Star } from "lucide-react";
import React from "react";
import { Question } from "survey-core";

interface RatingAnswerProps extends React.HTMLAttributes<HTMLDivElement> {
    question: Question;
}

const RatingAnswer: React.FC<RatingAnswerProps> = ({ question, ...props }) => {
    const minRating = question.rateMin;
    const maxRating = question.rateMax;
    const ratingStep = question.rateStep;
    const ratingValue = question.value;
    const ratingText = `${ratingValue} out of ${maxRating}`;
    const ratingScale = Array.from({ length: (maxRating - minRating) / ratingStep + 1 }, (_, i) => minRating + i * ratingStep);


    if (question.value === undefined) {
        return <Minus className="h-4 w-4" />
    }

    return (
        <div  {...props} className={cn("flex items-center gap-1", props.className)}>
            {ratingScale.map((scale, index) => (
                <React.Fragment key={index}>
                    {scale <= ratingValue ? (
                        <Star className="h-4 w-4 text-primary fill-primary cursor-not-allowed" />
                    ) : (
                        <Star className="h-4 w-4 text-primary cursor-not-allowed" />
                    )}
                </React.Fragment>
            ))}
            <span className="pt-2 text-sm text-muted-foreground">{ratingText}</span>
        </div>
    );
}

export default RatingAnswer;