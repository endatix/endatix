'use client';

import PageTitle from '@/components/headings/page-title';
import { Button } from '@/components/ui/button';
import {
  Download,
  FilePenLine,
  LinkIcon,
  Sparkles,
  Trash2,
} from 'lucide-react';
import Link from 'next/link';
import { SubmissionActionsDropdown } from './submission-actions-dropdown';
import { useState } from 'react';
import { Spinner } from '@/components/loaders/spinner';
import { saveToFileHandler } from 'survey-creator-core';

interface SubmissionHeaderProps {
  submissionId: string;
  formId: string;
}

export function SubmissionHeader({
  submissionId,
  formId,
}: SubmissionHeaderProps) {
  const [loading, setLoading] = useState(false);

  const exportPdf = async () => {
    console.log('Setting loading to true');
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
        setLoading(false);
      }
    } catch (error) {
      console.error('Failed to export PDF:', error);
    } finally {
      console.log('Setting loading to false');
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
            <Spinner className="mr-2 h-4 w-4" />
          ) : (
            <Download className="mr-2 h-4 w-4" />
          )}
          {loading ? 'Exporting...' : 'Export PDF'}
        </Button>

        <Button variant={'outline'} asChild>
          <Link href={`/share/${formId}`} target="_blank">
            <LinkIcon className="mr-2 h-4 w-4" />
            Share Link
          </Link>
        </Button>

        <Button variant={'outline'} asChild>
          <Link
            href={`/forms/${formId}/submissions/${submissionId}/edit`}
            className="hidden"
          >
            <FilePenLine className="mr-2 h-4 w-4" />
            Edit
          </Link>
        </Button>

        <Button variant={'outline'} asChild>
          <Link href="#" className="hidden">
            <Sparkles className="mr-2 h-4 w-4" />
            Mark as new
          </Link>
        </Button>
        
        <Button variant={'outline'} asChild>
          <Link href="#" className="hidden">
            <Trash2 className="mr-2 h-4 w-4" />
            Delete
          </Link>
        </Button>

        <SubmissionActionsDropdown
          formId={formId}
          submissionId={submissionId}
          className="text-muted-foreground"
        />
      </div>
    </div>
  );
}
