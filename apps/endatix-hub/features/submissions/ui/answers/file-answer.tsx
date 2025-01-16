import { ScrollArea, ScrollBar } from "@/components/ui/scroll-area";
import { cn } from "@/lib/utils";
import { QuestionFileModelBase } from "survey-core/typings/packages/survey-core/src/question_file";
import { File, FileViewer } from "./file-viewer";
import { MessageSquareText } from "lucide-react";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
interface FileAnswerProps extends React.HtmlHTMLAttributes<HTMLDivElement> {
  question: QuestionFileModelBase;
}

export function FileAnswer({ question, className, ...props }: FileAnswerProps) {
  const files = question?.value as File[];

  return (
    <div className={cn("col-span-5", className)} {...props}>
      <ScrollArea>
        <div className="flex items-center justify-start text-sm space-x-4 pb-1">
          {files &&
            files.map((file) => (
              <FileViewer
                key={file.name}
                file={file}
                question={question}
                className="w-max-[250px]"
                aspectRatio="portrait"
                width={250}
                height={330}
              />
            ))}
        </div>
        {question?.supportComment() && question.hasComment && (
          <div className="flex items-center justify-start">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger>
                  <MessageSquareText
                    aria-label="Comment"
                    className="w-4 h-4 mr-2"
                  />
                </TooltipTrigger>
                <TooltipContent>
                  <p>Comment</p>
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
            <span className="text-muted-foreground text-sm">
              {question.comment}
            </span>
          </div>
        )}
        <ScrollBar orientation="horizontal" />
      </ScrollArea>
    </div>
  );
}
