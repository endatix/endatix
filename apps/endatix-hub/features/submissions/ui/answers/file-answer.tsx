import { ScrollArea, ScrollBar } from "@/components/ui/scroll-area";
import { cn } from "@/lib/utils"
import { QuestionFileModelBase } from "survey-core/typings/packages/survey-core/src/question_file";
import { File, FileViewer } from "./file-viewer";

interface FileAnswerProps extends React.HtmlHTMLAttributes<HTMLDivElement> {
  question: QuestionFileModelBase
}

export function FileAnswer({
  question,
  className,
  ...props
}: FileAnswerProps) {
  const files = question?.value as File[];

  return (
    <div className={cn("col-span-5", className)} {...props}>
      <ScrollArea>
        <div className="flex items-center text-sm space-x-4 pb-4">
          {files && files.map((file) => (
            <FileViewer
              key={file.name}
              file={file}
              question={question}
              className="w-[250px]"
              aspectRatio="portrait"
              width={250}
              height={330}
            />
          ))}
        </div>
        <ScrollBar orientation="horizontal" />
      </ScrollArea>
    </div>);
}