import { Button } from "@/components/ui/button";
import {
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { DropdownMenu } from "@/components/ui/dropdown-menu";
import { Submission } from "@/types";
import { Row } from "@tanstack/react-table";
import {
  FileDown,
  FilePenLine,
  LinkIcon,
  MoreHorizontal,
  Trash2,
} from "lucide-react";
import Link from "next/link";
import { StatusDropdownMenuItem } from "@/features/submissions/use-cases/change-status";

interface RowActionsProps<TData> {
  row: Row<TData>;
}

export function RowActions<TData>({ row }: RowActionsProps<TData>) {
  const item = row.original as Submission;
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          onClick={(event) => event.stopPropagation()}
          aria-haspopup="true"
          size="icon"
          variant="ghost"
        >
          <MoreHorizontal className="h-4 w-4" />
          <span className="sr-only">Toggle menu</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent
        onClick={(event) => event.stopPropagation()}
        className="text-gray-600"
        align="start"
      >
        <DropdownMenuItem asChild className="cursor-pointer">
          <Link href={`/forms/${item.formId}/submissions/${item.id}/edit`}>
            <FilePenLine className="w-4 h-4 mr-2" />
            <span>Edit</span>
          </Link>
        </DropdownMenuItem>
        <StatusDropdownMenuItem
          submissionId={item.id}
          formId={item.formId}
          status={item.status}
        />
        <DropdownMenuSeparator />
        <DropdownMenuItem className="cursor-not-allowed">
          <FileDown className="w-4 h-4 mr-2" />
          <span>Export PDF</span>
        </DropdownMenuItem>
        <DropdownMenuItem asChild className="cursor-disabled">
          <Link href="#">
            <LinkIcon className="w-4 h-4 mr-2" />
            <span>Share Links</span>
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem className="cursor-not-allowed">
          <Trash2 className="w-4 h-4 mr-2" />
          <span>Delete</span>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
