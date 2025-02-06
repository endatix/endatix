import { ColumnDef } from "@tanstack/react-table";
import { Submission } from "@/types";
import { RowActions } from "./row-actions";
import { ColumnHeader } from "./column-header";
import { CellDate } from "./cell-date";
import { CellCompleteStatus } from "./cell-complete-status";
import { CellCompletionTime } from "./cell-completion-time";
import { CellReadStatus } from "./cell-read-status";

export const COLUMNS_DEFINITION: ColumnDef<Submission>[] = [
  {
    id: "actions",
    header: ({ column }) => (
      <ColumnHeader
        className="text-center hidden"
        column={column}
        title="Actions"
        visible={false}
      />
    ),
    cell: ({ row }) => <RowActions row={row} />,
  },
  {
    id: "createdAt",
    header: ({ column }) => (
      <ColumnHeader
        className="hidden md:table-cell"
        column={column}
        title="Created at"
      />
    ),
    cell: ({ row }) => <CellDate date={row.original.createdAt} />,
  },
  {
    id: "complete",
    header: ({ column }) => (
      <ColumnHeader column={column} title="Is Complete" />
    ),
    cell: ({ row }) => (
      <CellCompleteStatus isComplete={row.original.isComplete} />
    ),
  },
  {
    id: "completedAt",
    header: ({ column }) => (
      <ColumnHeader column={column} title="Completed at" />
    ),
    cell: ({ row }) => (
      <CellDate
        date={row.original.completedAt}
        visible={row.original.isComplete}
      />
    ),
  },
  {
    id: "completionTime",
    header: ({ column }) => (
      <ColumnHeader column={column} title="Completion Time" />
    ),
    cell: ({ row }) => (
      <CellCompletionTime
        startedAt={row.original.createdAt}
        completedAt={row.original.completedAt}
      />
    ),
  },
  {
    id: "status",
    header: ({ column }) => <ColumnHeader column={column} title="Status" />,
    cell: ({ row }) => <CellReadStatus code={row.original.status} />,
  },
];
