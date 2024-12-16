import Image from "next/image"
import { cn } from "@/lib/utils"
import { QuestionFileModelBase } from "survey-core/typings/packages/survey-core/src/question_file"


export interface File {
    content: string,
    name?: string,
    type?: string,
  }

interface FileViewerProps extends React.HTMLAttributes<HTMLDivElement> {
    file: File
    question: QuestionFileModelBase
    width?: number
    height?: number
    aspectRatio?: "portrait" | "square"
}

export function FileViewer({
    file,
    width,
    height,
    question,
    className,
    aspectRatio = "portrait",
    ...props
}: FileViewerProps) {

    const isImage = question.isFileImage(file);

    return (
        <div className={cn("space-y-3", className)} {...props}>
            <div className="overflow-hidden rounded-md">
                {isImage ?
                    <Image
                        src={file.content}
                        alt={file.name || ''}
                        width={width}
                        height={height}
                        className={cn(
                            "h-auto w-auto object-cover transition-all hover:scale-105",
                            aspectRatio === "portrait" ? "aspect-[3/4]" : "aspect-square"
                        )}
                    />
                    :
                    <div className="h-[330px] w-[250px] bg-gray-200">Document with name {file.name}</div>
                }
            </div>
            <div className="space-y-1 text-sm">
                <h3 className="font-medium leading-none">{file.name}</h3>
            </div>
        </div>
    )
}