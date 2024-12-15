"use client"

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { TableCell, TableRow } from "@/components/ui/table";
import { Submission } from "@/types";
import { FileDown, FilePenLine, LinkIcon, MoreHorizontal, Sparkles, Trash2 } from "lucide-react";
import { redirect } from "next/navigation";
import { useMemo } from "react";

type SubmissionsRowProps = {
    item: Submission;
    onClick: () => void;
    isSelected: boolean;
};

const getStatusLabel = (isComplete: boolean) => isComplete ? "Yes" : "No";

const getSeenLabel = (isComplete: boolean) => isComplete ? "Seen" : "New";

const getCompletionTime = (startedAt: Date, completedAt: Date): string => {
    if (!startedAt || !completedAt) return "-";
    if (completedAt < startedAt) return "-";

    const diff = new Date(completedAt).getTime() - new Date(startedAt).getTime();
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const mins = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
    const secs = Math.floor((diff % (1000 * 60)) / 1000);

    return `${hours.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
}

const SubmissionRow = ({ item, onClick, isSelected }: SubmissionsRowProps) => {
    const rowClassName = useMemo(() => {
        let className: string = "";

        if (isSelected) {
            className += "bg-accent";
        }
        if (item.isComplete) {
            className += "font-medium";
        }
        return className;
    }, [isSelected, item.isComplete]);

    return (
        <TableRow key={item.id} onClick={onClick} className={rowClassName}>
            <TableCell>
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
                            <span>Mark as seen</span>
                        </DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem>
                            <FileDown className="w-4 h-4 mr-2" />
                            <span>Export PDF</span>
                        </DropdownMenuItem>
                        <DropdownMenuItem onClick={() => {redirect(`/share/${item.formId}`)}}>
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
            </TableCell>
            <TableCell className="hidden text-center">{item.id}</TableCell>
            <TableCell className="hidden md:table-cell text-center">
                {new Date(item.createdAt).toLocaleString("en-US")}
            </TableCell>
            <TableCell className="text-center">
                <Badge variant="outline">
                    {getStatusLabel(item.isComplete)}
                </Badge>
            </TableCell>
            <TableCell className="text-center">
                {item.isComplete && new Date(item.completedAt).toLocaleString("en-US")}
            </TableCell>
            <TableCell className="hidden md:table-cell text-center">
                {getCompletionTime(item.createdAt, item.completedAt)}
            </TableCell>
            <TableCell className="text-center">
                <Badge variant={item.isComplete ? "secondary" : "default"}>
                    {getSeenLabel(item.isComplete)}
                </Badge>
            </TableCell>
        </TableRow>
    );
}

export default SubmissionRow;