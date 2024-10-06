import { cn } from "@/lib/utils";
import { Star } from "lucide-react";
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


    return (
        <>
            <div  {...props} className={cn("flex items-center gap-2", props.className)}>
                {ratingScale.map((scale, index) => (
                    <React.Fragment key={index}>
                        {scale <= ratingValue ? (
                            <Star className="text-primary fill-primary" />
                        ) : (
                            <Star className="text-primary" />
                        )}
                    </React.Fragment>
                ))}
               <span className="pt-4 text-sm text-muted-foreground">{ratingText}</span>
            </div>
        </>
    );
}

export default RatingAnswer;