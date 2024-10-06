import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { cn } from "@/lib/utils";
import React from "react";
import { Question } from "survey-core";

interface DropdownAnswerProps extends React.HtmlHTMLAttributes<HTMLDivElement> {
    question: Question;
  }
  const DropdownAnswer = ({
    question,
    className,
    ...props
  }: DropdownAnswerProps) => {
    const text = React.useMemo(() => {
      const value = question.value;
      let text = value;
  
      const selectedChoice = question.choices.find((c) => c.value === 2);
      if (selectedChoice) {
        text = selectedChoice.title;
      }
  
      return text;
    }, [question]);
  
    return (
      <Select disabled>
        <SelectTrigger className={cn("w-[180px]", className)}>
          <SelectValue placeholder={text} />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value={question.value}>{text}</SelectItem>
        </SelectContent>
      </Select>
    );
  };

  export default DropdownAnswer;