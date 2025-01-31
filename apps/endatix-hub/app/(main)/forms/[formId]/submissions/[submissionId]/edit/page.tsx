import { SectionTitle } from '@/components/headings/section-title';
import { Suspense } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { getSubmissionDetailsUseCase } from '@/features/submissions/use-cases/get-submission-details.use-case';
import { BackToSubmissionsButton } from '@/features/submissions/ui/details/back-to-submissions-button';
import { Result } from '@/lib/result';
import EditSubmission from '@/features/submissions/ui/edit/edit-submission';

type Params = {
  params: Promise<{
    formId: string;
    submissionId: string;
  }>;
};

export default async function EditSubmissionPage({ params }: Params) {
  const { formId, submissionId } = await params;

  const submissionResult = await getSubmissionDetailsUseCase({
    formId,
    submissionId,
  });

  if (
    Result.isError(submissionResult) ||
    !submissionResult.value?.formDefinition
  ) {
    return (
      <div>
        <h1>Submission not found</h1>
        <BackToSubmissionsButton
          formId={formId}
          text="All form submissions"
          variant="default"
        />
      </div>
    );
  }
  const submission = submissionResult.value;

  return (
    <Suspense fallback={<SubmissionDataSkeleton />}>
      <EditSubmission submission={submission} />
    </Suspense>
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
