import { TooltipContent } from "@/components/ui/tooltip";
import { TooltipTrigger } from "@/components/ui/tooltip";
import { Tooltip } from "@/components/ui/tooltip";
import { TooltipProvider } from "@/components/ui/tooltip";
import { FileIcon, Info, MessageSquareTextIcon } from "lucide-react";
import { Question } from "survey-core";

const QuestionComment = ({ comment }: { comment: string }) => {
  return (
    <div className="flex flex-row items-start gap-2">
      <MessageSquareTextIcon className="h-4 w-4 text-muted-foreground" />
      <span className="text-muted-foreground text-left">{comment}</span>
    </div>
  );
};

const ChangedQuestion = ({ question }: { question: Question }) => {
  if (!question) return null;

  const QuestionWrapper = ({ children }: { children: React.ReactNode }) => (
    <div className="flex flex-row items-start gap-2 text-sm">
      <div className="flex flex-row items-center justify-end gap-2 w-1/2">
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Info className="h-4 w-4 hidden md:block" />
            </TooltipTrigger>
            <TooltipContent>{question.title}</TooltipContent>
          </Tooltip>
        </TooltipProvider>
        <span className="font-medium">{question.name} :</span>
      </div>
      <div className="flex flex-col items-start gap-0">
        {children}
        {question.hasComment && <QuestionComment comment={question.comment} />}
      </div>
    </div>
  );

  const renderValue = () => {
    switch (question.getType()) {
      case "text":
      case "comment":
      case "dropdown":
        return <div className="text-muted-foreground">{question.value}</div>;

      case "select":
        return (
          <div className="text-muted-foreground">
            {Array.isArray(question.value)
              ? question.value.join(", ")
              : question.value}
          </div>
        );

      case "boolean":
      case "checkbox":
        return (
          <span className="text-muted-foreground">
            {question.value ? "Yes" : "No"}
          </span>
        );

      case "file":
        return (
          <div className="flex flex-row items-start gap-2">
            <FileIcon className="h-4 w-4" />
            <span className="text-muted-foreground">
              has {question.value.length}{" "}
              {question.value.length === 1 ? "file" : "files"}
            </span>
          </div>
        );

      default:
        return (
          <span className="text-muted-foreground">
            {String(question.value)}
          </span>
        );
    }
  };

  return <QuestionWrapper>{renderValue()}</QuestionWrapper>;
};

export default ChangedQuestion;
