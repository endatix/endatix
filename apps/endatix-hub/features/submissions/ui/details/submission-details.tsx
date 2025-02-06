'use server';

import { getSubmissionDetailsUseCase } from '@/features/submissions/use-cases/get-submission-details.use-case';
import { Result } from '@/lib/result';
import { Model } from 'survey-core';
import { SubmissionProperties } from './submission-properties';
import AnswerViewer from '../answers/answer-viewer';
import { SectionTitle } from '@/components/headings/section-title';
import { BackToSubmissionsButton } from './back-to-submissions-button';
import { SubmissionHeader } from './submission-header';

async function SubmissionDetails({
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
      <SubmissionHeader
        formId={formId}
        submissionId={submissionId}
        status={submission.status}
      />
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

export default SubmissionDetails;
