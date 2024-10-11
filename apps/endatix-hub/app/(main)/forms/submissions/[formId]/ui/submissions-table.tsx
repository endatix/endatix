"use client"

import {
  Table,
  TableBody,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Submission } from "@/types";
import SubmissionRow from "./submission-row";
import { Suspense, useEffect, useMemo, useState } from "react";
import { Skeleton } from "@/components/ui/skeleton";
import SubmissionSheet from "./submission-sheet";

type SubmissionsTableProps = {
  data: Submission[];
};

const SubmissionsTable = ({ data }: SubmissionsTableProps) => {
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

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Is Complete</TableHead>
            <TableHead>Completion Time</TableHead>
            <TableHead>Created at</TableHead>
            <TableHead className="hidden md:table-cell">Completed at</TableHead>
            <TableHead>
              <span className="sr-only">Actions</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {data.map((item) => (
            <Suspense key={item.id} fallback={<LoadingFallback />} >
              <SubmissionRow isSelected={item.id === selectedSubmissionId} onClick={() => setSelectedSubmissionId(item.id)} item={item} />
            </Suspense>
          ))}
        </TableBody>

        {selectedSubmission && (
          <SubmissionSheet submission={selectedSubmission} />
        )}
      </Table>
    </>
  );
}

const LoadingFallback = () => (
  <div className="flex flex-row">
    <div className="space-y-2">
      <Skeleton className="h-4 w-[250px]" />
      <Skeleton className="h-4 w-[200px]" />
      <Skeleton className="h-4 w-[200px]" />
    </div>
  </div>
)

export default SubmissionsTable;
