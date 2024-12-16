import { HTMLAttributes } from "react";
import { Separator } from "../ui/separator";
import { cn } from "@/lib/utils";

interface SectionTitleProps extends HTMLAttributes<HTMLDivElement> {
    title: string;
    headingClassName?: string;
    withSeparator?: boolean;
}

export function SectionTitle({
    title,
    withSeparator = true,
    headingClassName,
    ...props
}: SectionTitleProps) {
    return (
        <div {...props}>
            <h2 className={cn("text-2xl font-medium tracking-tight", headingClassName)}>
                {title}
            </h2>
            {withSeparator && <Separator className="mb-4" />}
        </div>
    )
}