import { Label } from "@/components/ui/label";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Minus } from "lucide-react";
import { Question } from "survey-core";

interface RadioGroupAnswerProps
  extends React.HtmlHTMLAttributes<HTMLDivElement> {
  question: Question;
}
const RadioGroupAnswer = ({ question, className }: RadioGroupAnswerProps) => {
  if (question.value === undefined) {
    return <Minus className="h-4 w-4" />;
  }

  return (
    <RadioGroup disabled defaultValue={question.value} className={className}>
      <div className="flex items-center space-x-2">
        <RadioGroupItem value={question.value} id={question.id} />
        <Label htmlFor={question.id}>{question.value}</Label>
      </div>
    </RadioGroup>
  );
};

export default RadioGroupAnswer;
