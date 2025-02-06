'use client';

import PageTitle from '@/components/headings/page-title';
import { Button } from '@/components/ui/button';
import { Download, FilePenLine } from 'lucide-react';
import Link from 'next/link';
import { SubmissionActionsDropdown } from './submission-actions-dropdown';
import { useState } from 'react';
import { Spinner } from '@/components/loaders/spinner';
import { saveToFileHandler } from 'survey-creator-core';
import { toast } from 'sonner';
import { StatusButton } from '@/features/submissions/use-cases/change-status';

interface SubmissionHeaderProps {
  submissionId: string;
  formId: string;
  status: string;
}

export function SubmissionHeader({
  submissionId,
  formId,
  status,
}: SubmissionHeaderProps) {
  const [loading, setLoading] = useState(false);

  const exportPdf = async () => {
    setLoading(true);
    try {
      const url = `/api/public/v0/forms/${formId}/submissions/${submissionId}/export-pdf`;
      const pdfFileName = `submission-${submissionId}.pdf`;
      const fileResponse = await fetch(url);
      if (fileResponse.ok) {
        const blob = new Blob([await fileResponse.arrayBuffer()], {
          type: 'text/plain;charset=utf-8',
        });
        saveToFileHandler(pdfFileName, blob);
        toast.success('PDF exported successfully');
        setLoading(false);
      }
    } catch (error) {
      console.error('Failed to export PDF:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="my-2 flex flex-col gap-6 sm:gap-2 sm:flex-row justify-between">
      <PageTitle title="Submission Details" />
      <div className="flex space-x-2 justify-end text-muted-foreground">
        <Button
          variant={'outline'}
          onClick={() => exportPdf()}
          disabled={loading}
        >
          {loading ? (
            <Spinner className="h-4 w-4" />
          ) : (
            <Download className="h-4 w-4" />
          )}
          {loading ? 'Exporting...' : 'Export PDF'}
        </Button>

        <StatusButton
          className="hidden md:flex"
          submissionId={submissionId}
          formId={formId}
          status={status}
        />

        <Button variant={'outline'} asChild className="hidden md:flex">
          <Link href={`/forms/${formId}/submissions/${submissionId}/edit`}>
            <FilePenLine className="h-4 w-4" />
            Edit
          </Link>
        </Button>

        <SubmissionActionsDropdown
          formId={formId}
          submissionId={submissionId}
          status={status}
          className="text-muted-foreground"
        />
      </div>
    </div>
  );
}
