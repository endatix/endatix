'use client'

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ColumnDef, flexRender, getCoreRowModel, getPaginationRowModel, useReactTable } from '@tanstack/react-table'
import { Submission } from "@/types";
import SubmissionRow from "./submission-row";
import { useEffect, useMemo, useState } from "react";
import SubmissionSheet from "./submission-sheet";
import { TablePagination } from "./table-pagination";
import TableRowActions from "./table-row-actions";

type SubmissionsTableProps = {
  data: Submission[];
  renderNewTable?: boolean;
};

const columns: ColumnDef<Submission>[] = [
  {
    id: "actions",
    cell: ({ row }) => <TableRowActions row={row} />
  },
  {
    header: "Created at",
    accessorKey: "createdAt",
    enableSorting: true,
    enableHiding: true,
  },
  {
    header: "Complete?",
    accessorKey: "complete",
    enableSorting: true,
    enableHiding: true,
  },
  {
    header: "Completed at",
    accessorKey: "completedAt",
    enableSorting: true,
    enableHiding: true,
  },
  {
    header: "Completion Time",
    accessorKey: "completionTime",
    enableSorting: true,
    enableHiding: true,
  },
  {
    header: "Status",
    accessorKey: "status",
    enableSorting: true,
    enableHiding: true,
  },
]

const SubmissionsTable = ({
  data,
  renderNewTable
}: SubmissionsTableProps) => {
  const [selectedSubmissionId, setSelectedSubmissionId] = useState<string | null>(null);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (!selectedSubmissionId) {
        return;
      }

      if (e.key === "Escape") {
        setSelectedSubmissionId(null); // Deselect
        return;
      }

      const currentIndex = data.findIndex(s => s.id === selectedSubmissionId);
      if (e.key === "ArrowUp" || e.key === "ArrowRight") {
        const prevIndex = (currentIndex > 0 ? currentIndex - 1 : data.length - 1);
        setSelectedSubmissionId(data[prevIndex].id);
      } else if (e.key === "ArrowDown" || e.key === "ArrowLeft") {
        const nextIndex = (currentIndex < data.length - 1 ? currentIndex + 1 : 0);
        setSelectedSubmissionId(data[nextIndex].id);
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [selectedSubmissionId, data]);

  const selectedSubmission = useMemo(
    () => data.find(s => s.id === selectedSubmissionId),
    [selectedSubmissionId, data]
  );

  if (renderNewTable) {
    return (
      <DataTable
        data={data}
        columns={columns} />
    )
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>
            <span className="sr-only">Actions</span>
          </TableHead>
          <TableHead className="text-center hidden">ID</TableHead>
          <TableHead className="text-center hidden md:table-cell">Created at</TableHead>
          <TableHead className="text-center">Complete?</TableHead>
          <TableHead className="text-center">Completed at</TableHead>
          <TableHead className="text-center">Completion Time</TableHead>
          <TableHead className="text-center">Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {data.map((item: Submission) => (
          <SubmissionRow
            key={item.id}
            isSelected={item.id === selectedSubmissionId}
            onClick={() => setSelectedSubmissionId(item.id)}
            item={item} />
        ))}
      </TableBody>

      {selectedSubmission && (
        <SubmissionSheet submission={selectedSubmission} />
      )}
    </Table>
  );
}

const DataTable = ({
  data,
  columns }: {
    data: Submission[];
    columns: ColumnDef<Submission>[];
  }) => {

  const table = useReactTable({
    data,
    columns,
    debugTable: true,
    getCoreRowModel: getCoreRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
  })

  return (
    <>
      <Table>
        <TableHeader>
          {table.getHeaderGroups().map((headerGroup) => (
            <TableRow key={headerGroup.id}>
              {headerGroup.headers.map((header) => {
                return (
                  <TableHead key={header.id} colSpan={header.colSpan}>
                    {header.isPlaceholder
                      ? null
                      : flexRender(
                        header.column.columnDef.header,
                        header.getContext()
                      )}
                  </TableHead>
                )
              })}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {table.getRowModel().rows?.length ? (
            table.getRowModel().rows.map((row) => (
              <TableRow
                key={row.id}
                data-state={row.getIsSelected() && "selected"}
              >
                {row.getVisibleCells().map((cell) => (
                  <TableCell key={cell.id}>
                    {flexRender(
                      cell.column.columnDef.cell,
                      cell.getContext()
                    )}
                  </TableCell>
                ))}
              </TableRow>
            ))
          ) : (
            <TableRow>
              <TableCell
                colSpan={columns.length}
                className="h-24 text-center"
              >
                No results.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
      <TablePagination table={table} />
    </>
  )
}


export default SubmissionsTable;
