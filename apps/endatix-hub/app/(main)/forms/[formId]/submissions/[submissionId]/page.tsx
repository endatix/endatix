import AnswerViewer from '@/features/submissions/ui/answers/answer-viewer';
import { SubmissionHeader } from '@/features/submissions/ui/details/submission-header';
import { SubmissionProperties } from '@/features/submissions/ui/details/submission-properties';
import { SubmissionTopNav } from '@/features/submissions/ui/details/submission-top-nav';
import { getSubmissionDetailsUseCase } from '@/features/submissions/use-cases/get-submission-details.use-case';
import { Result } from '@/lib/result';
import { Model } from 'survey-core';
import { SectionTitle } from '@/components/headings/section-title';
import { BackToSubmissionsButton } from '@/features/submissions/ui/details/back-to-submissions-button';
import { Suspense } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { pdf } from '@react-pdf/renderer';
import { SubmissionDataPdf } from '@/components/export/submission-data-pdf';

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
        <SubmissionData formId={formId} submissionId={submissionId} />
      </Suspense>
    </>
  );
}

async function SubmissionData({
  formId,
  submissionId,
}: {
  formId: string;
  submissionId: string;
}) {
  const submissionResult = await getSubmissionDetailsUseCase({
    formId,
    submissionId,
  });

  if (Result.isError(submissionResult)) {
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
  if (!submission.formDefinition) {
    return <div>Form definition not found</div>;
  }
  const json = JSON.parse(submission.formDefinition.jsonData);
  const surveyModel = new Model(json);

  let submissionData = {};
  try {
    submissionData = JSON.parse(submission?.jsonData);
  } catch (ex) {
    console.warn("Error while parsing submission's JSON data", ex);
  }

  surveyModel.data = submissionData;
  const questions = surveyModel.getAllQuestions(false, false, true);

  return (
    <>
      <SubmissionProperties submission={submission} />
      <SectionTitle title="Submission Answers" headingClassName="py-2 my-0" />
      <div className="grid gap-4">
        {questions?.map((question) => {
          return (
            <div
              key={question.id}
              className="grid grid-cols-5 items-center gap-4 mb-6"
            >
              <AnswerViewer key={question.id} forQuestion={question} />
            </div>
          );
        })}
      </div>
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
