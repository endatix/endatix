import { SubmissionHeader } from '@/features/submissions/ui/details/submission-header';
import { SubmissionTopNav } from '@/features/submissions/ui/details/submission-top-nav';
import { getSubmissionDetailsUseCase } from '@/features/submissions/use-cases/get-submission-details.use-case';
import { Result } from '@/lib/result';
import { SectionTitle } from '@/components/headings/section-title';
import { Suspense } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { pdf } from '@react-pdf/renderer';
import { SubmissionDataPdf } from '@/components/export/submission-data-pdf';
import SubmissionDetails from '@/features/submissions/ui/details/submission-details';

type Params = {
  params: Promise<{
    formId: string;
    submissionId: string;
  }>;
  searchParams: Promise<{
    format: string;
  }>;
};

export default async function SubmissionPage({ params, searchParams }: Params) {
  const { formId, submissionId } = await params;
  const { format } = await searchParams;

  if (format?.toLowerCase() === 'pdf') {
    const submissionResult = await getSubmissionDetailsUseCase({
      formId,
      submissionId,
    });

    if (Result.isError(submissionResult)) {
      return <div>Submission not found</div>;
    }

    const submission = submissionResult.value;
    const pdfBlob = await pdf(
      <SubmissionDataPdf submission={submission} />
    ).toBlob();
    const buffer = Buffer.from(await pdfBlob.arrayBuffer());

    const pdfBase64Url = `data:application/pdf;base64,${buffer.toString(
      'base64'
    )}`;

    return (
      <div className="flex justify-center items-center h-screen">
        <embed
          src={pdfBase64Url}
          type="application/pdf"
          width="100%"
          height="100%"
          style={{ border: 'none' }}
          title="Submission PDF Viewer"
        />
      </div>
    );
  }

  return (
    <>
      <SubmissionTopNav formId={formId} />
      <SubmissionHeader submissionId={submissionId} formId={formId} />
      <Suspense fallback={<SubmissionDataSkeleton />}>
        <SubmissionDetails formId={formId} submissionId={submissionId} />
      </Suspense>
    </>
  );
}

function SubmissionDataSkeleton() {
  const summaryQuestions = Array.from({ length: 5 }, (_, index) => index + 1);
  const answersQuestions = Array.from({ length: 16 }, (_, index) => index + 1);

  return (
    <div className="w-full overflow-auto">
      <div className="flex flex-col space-y-2 items-center">
        {summaryQuestions.map((question) => (
          <Skeleton className="h-8 w-[300px] " key={question} />
        ))}
      </div>
      <SectionTitle title="Submission Answers" headingClassName="py-2 my-0" />
      <div className="flex flex-col items-center space-y-2 items-center">
        {answersQuestions.map((question) => (
          <Skeleton className="h-12 w-[600px]" key={question} />
        ))}
      </div>
    </div>
  );
}
