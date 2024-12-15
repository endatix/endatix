import { Button } from "@/components/ui/button"
import { DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu"
import { DropdownMenu } from "@/components/ui/dropdown-menu"
import { Submission } from "@/types"
import { Row } from "@tanstack/react-table"
import { FileDown, FilePenLine, LinkIcon, MoreHorizontal, Sparkles, Trash2 } from "lucide-react"
import { redirect } from "next/navigation"

interface RowActionsProps<TData> {
    row: Row<TData>
}

export function RowActions<TData>({
    row
}: RowActionsProps<TData>) {
    const item = row.original as Submission;
    return (
        <DropdownMenu>
            <DropdownMenuTrigger asChild>
                <Button aria-haspopup="true" size="icon" variant="ghost">
                    <MoreHorizontal className="h-4 w-4" />
                    <span className="sr-only">Toggle menu</span>
                </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent className="text-gray-600" align="start">
                <DropdownMenuItem>
                    <FilePenLine className="w-4 h-4 mr-2" />
                    <span>Edit</span>
                </DropdownMenuItem>
                <DropdownMenuItem>
                    <Sparkles className="w-4 h-4 mr-2" />
                    <span>Mark as new</span>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem>
                    <FileDown className="w-4 h-4 mr-2" />
                    <span>Export PDF</span>
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => { redirect(`/share/${item.formId}`) }}>
                    <LinkIcon className="w-4 h-4 mr-2" />
                    <span>Share Link</span>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem>
                    <Trash2 className="w-4 h-4 mr-2" />
                    <span>Delete</span>
                </DropdownMenuItem>
            </DropdownMenuContent>
        </DropdownMenu>
    )
}