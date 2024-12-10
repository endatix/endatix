"use client"

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { TableCell, TableRow } from "@/components/ui/table";
import { Submission } from "@/types";
import { MoreHorizontal } from "lucide-react";
import Link from "next/link";
import { useMemo } from "react";

type SubmissionsRowProps = {
    item: Submission;
    onClick: () => void;
    isSelected: boolean;
};

const getStatusLabel = (isComplete: boolean) => isComplete ? "Yes" : "No";

const getSeenLabel= (isComplete: boolean) => isComplete ? "Seen" : "New";

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
        let className : string = "";
        
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
                    <DropdownMenuContent align="end">
                        <DropdownMenuLabel>Actions</DropdownMenuLabel>
                        <DropdownMenuItem>Edit</DropdownMenuItem>
                        <DropdownMenuItem>Delete</DropdownMenuItem>
                        <DropdownMenuItem>
                            <Link href={`/submissions/${item.id}`}>View Response</Link>
                        </DropdownMenuItem>
                    </DropdownMenuContent>
                </DropdownMenu>
            </TableCell>
            <TableCell className="hidden">{item.id}</TableCell>
            <TableCell className="hidden md:table-cell">
                {new Date(item.createdAt).toLocaleString("en-US")}
            </TableCell>
            <TableCell>
                <Badge variant="outline">
                    {getStatusLabel(item.isComplete)}
                </Badge>
            </TableCell>
            <TableCell>
                {item.isComplete && new Date(item.completedAt).toLocaleString("en-US")}
            </TableCell>
            <TableCell className="hidden md:table-cell">
                {getCompletionTime(item.createdAt, item.completedAt)}
            </TableCell>
            <TableCell>
                <Badge variant={item.isComplete ? "secondary" : "default"}>
                    {getSeenLabel(item.isComplete)}
                </Badge>
            </TableCell>
        </TableRow>
    );
}

export default SubmissionRow;