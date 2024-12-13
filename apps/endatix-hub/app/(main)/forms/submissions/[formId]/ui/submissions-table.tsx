'use client'

import {
  Table,
  TableBody,
  TableHead,
  TableHeader,
  TableRow
} from "@/components/ui/table";
import {
  COLUMNS_DEFINITION,
  DataTable,
} from "@/features/submissions/ui/table";
import { Submission } from "@/types";
import SubmissionRow from "./submission-row";
import { useEffect, useMemo, useState } from "react";
import SubmissionSheet from "./submission-sheet";


type SubmissionsTableProps = {
  data: Submission[];
  useLegacyTable?: boolean;
};

const SubmissionsTable = ({
  data,
  useLegacyTable
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

  if (useLegacyTable) {
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

  return (
    <DataTable
      data={data}
      columns={COLUMNS_DEFINITION} />
  )
}

export default SubmissionsTable;
