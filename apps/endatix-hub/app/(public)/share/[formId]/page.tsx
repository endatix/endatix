"use server"

import SurveyJsWrapper from '../../../../features/public-form/ui/survey-js-wrapper';
import { cookies } from 'next/headers';
import { FormTokenCookieStore } from '../../../../features/public-form/infrastructure/cookie-store';
import { Result } from '@/lib/result';
import { getActiveDefinitionUseCase } from '@/features/public-form/use-cases/get-active-definition.use-case';
import { getPartialSubmissionUseCase } from '@/features/public-form/use-cases/get-partial-submission.use-case';

type ShareSurveyPageProps = {
  params: Promise<{ formId: string }>
};

async function ShareSurveyPage({ params }: ShareSurveyPageProps) {
  const { formId } = await params;
  const cookieStore = await cookies();
  const tokenStore = new FormTokenCookieStore(cookieStore);

  const [submissionResult, activeDefinitionResult] = await Promise.all([
    getPartialSubmissionUseCase({ formId, tokenStore }),
    getActiveDefinitionUseCase({ formId })
  ]);

  const submission = Result.isSuccess(submissionResult) ? submissionResult.value : undefined;

  if (Result.isError(activeDefinitionResult)) {
    return <div>Form not found</div>;
  }

  const definition = activeDefinitionResult.value;

  return (
    <div className="flex min-h-screen flex-col items-center p-8">
      <SurveyJsWrapper
        formId={formId}
        definition={definition}
        submission={submission}
      />
    </div>
  );
}

export default ShareSurveyPage;