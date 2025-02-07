import { ScrollArea, ScrollBar } from "@/components/ui/scroll-area";
import { cn } from "@/lib/utils";
import { QuestionFileModel } from "survey-core";
import { File } from "@/lib/questions/file/file-type";
import { FileViewer } from "./file-viewer";
import { ImageOff, MessageSquareText } from "lucide-react";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
interface FileAnswerProps extends React.HtmlHTMLAttributes<HTMLDivElement> {
  question: QuestionFileModel;
}

export function FileAnswer({ question, className, ...props }: FileAnswerProps) {
  const files: File[] = Array.isArray(question?.value) ? question?.value : [];

  if (files.length === 0) {
    return (
      <div className={cn("col-span-5", className)} {...props}>
        <div className="flex items-center justify-start text-sm text-muted-foreground space-x-4 pb-1">
          <ImageOff className="w-4 h-4 mr-2" />
          No files uploaded
        </div>
      </div>
    );
  }

  return (
    <div className={cn("col-span-5", className)} {...props}>
      <ScrollArea>
        <div className="flex items-center justify-start text-sm space-x-4 pb-1">
          {files &&
            files.map((file, index) => (
              <FileViewer
                key={index}
                file={file}
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
