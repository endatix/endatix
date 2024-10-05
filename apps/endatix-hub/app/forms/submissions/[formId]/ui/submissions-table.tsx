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
import { Suspense, useActionState, useEffect, useState } from "react";
import { Skeleton } from "@/components/ui/skeleton";
import SubmissionSheet from "./submission-sheet";

type SubmissionsTableProps = {
  data: Submission[];
};

const SubmissionsTable = ({ data }: SubmissionsTableProps) => {
  const [selectedSubmission, setSelectedSubmission] = useState<Submission | null>(null);

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
              <SubmissionRow onClick={() => setSelectedSubmission(item)} item={item} />
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
