import { ColumnDef } from "@tanstack/react-table";
import { Submission } from "@/types";
import TableRowActions from "@/app/(main)/forms/submissions/[formId]/ui/table-row-actions";
import { ColumnHeader } from "./column-header";

export const COLUMNS_DEFINITION: ColumnDef<Submission>[] = [
    {
      id: "actions",
      header: ({ column }) => <ColumnHeader
        className="text-center hidden"
        column={column}
        title="Actions"
        visible={false} />,
      cell: ({ row }) => <TableRowActions row={row} />
    },
    {
      id: "createdAt",
      header: ({ column }) => <ColumnHeader
        className="hidden md:table-cell"
        column={column}
        title="Created at" />,
      accessorKey: "createdAt"
    },
    {
      header: ({ column }) => <ColumnHeader
        column={column}
        title="Is Complete" />,
      accessorKey: "complete"
    },
    {
      header: ({ column }) => <ColumnHeader
        column={column}
        title="Completed at" />,
      accessorKey: "completedAt"
    },
    {
      header: ({ column }) => <ColumnHeader
        column={column}
        title="Completion Time" />,
      accessorKey: "completionTime"
    },
    {
      header: ({ column }) => <ColumnHeader
        column={column}
        title="Status" />,
      accessorKey: "status"
    },
  ]