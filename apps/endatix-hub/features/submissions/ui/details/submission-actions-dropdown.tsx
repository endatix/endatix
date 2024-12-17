'use client'

import { Button, ButtonProps } from "@/components/ui/button"
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuTrigger
} from "@/components/ui/dropdown-menu"
import { cn } from "@/lib/utils"
import {
    MoreHorizontal,
    FilePenLine,
    Sparkles,
    Trash2
} from "lucide-react"

interface SubmissionActionsDropdownProps extends ButtonProps {
    submissionId: string
}

export function SubmissionActionsDropdown({ 
    submissionId,
    ...props
 }: SubmissionActionsDropdownProps) {
    return (
        <DropdownMenu>
            <DropdownMenuTrigger asChild>
                <Button {...props} className={cn("flex pl-2 pr-2 items-center", props.className)} aria-haspopup="true" variant="outline">
                    <MoreHorizontal className="h-6 w-6" />
                    <span className="sr-only">Toggle menu</span>
                </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent className="text-gray-600" align="end">
                <DropdownMenuItem>
                    <FilePenLine className="w-4 h-4 mr-2" />
                    <span>Edit</span>
                </DropdownMenuItem>
                <DropdownMenuItem>
                    <Sparkles className="w-4 h-4 mr-2" />
                    <span>Mark as new</span>
                </DropdownMenuItem>
                <DropdownMenuItem>
                    <Trash2 className="w-4 h-4 mr-2" />
                    <span>Delete</span>
                </DropdownMenuItem>
            </DropdownMenuContent>
        </DropdownMenu>
    )
}