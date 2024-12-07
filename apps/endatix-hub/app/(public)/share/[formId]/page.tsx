"use server"

import { FormDefinition, Submission } from '@/types';
import { getActiveFormDefinition, getExistingSubmission } from "@/services/api";
import SurveyJsWrapper from './ui/survey-js-wrapper';
import { cookies } from 'next/headers';
import { deleteTokenFromCookie, getTokenFromCookie, TOKENS_COOKIE_OPTIONS } from './lib/cookie-store';
import { Result } from '@/lib/result';
import { ReadonlyRequestCookies } from 'next/dist/server/web/spec-extension/adapters/request-cookies';

type ShareSurveyPageProps = {
  params: Promise<{ formId: string }>
};

async function ShareSurveyPage({ params }: ShareSurveyPageProps) {
  const { formId } = await params;
  const cookieStore = await cookies();
  const partialSubmissionKeysCookie = cookieStore.get(TOKENS_COOKIE_OPTIONS.name);
  const tokenResult = getTokenFromCookie(partialSubmissionKeysCookie, formId);

  const [submission, surveyJson] = await Promise.all([
    Result.isSuccess(tokenResult) ? getPartialSubmission(formId, tokenResult.value, cookieStore) : null,
    getSurveyJson(formId)
  ]);

  if (!surveyJson) {
    return <div>Form not found</div>;
  }

  return (
    <div className="flex min-h-screen flex-col items-center p-8">
      <SurveyJsWrapper
        formId={formId}
        definition={surveyJson}
        submission={submission ?? undefined}
      />
    </div>
  );
}

const getSurveyJson = async (formId: string) => {
  let formJson: string | null = null;

  try {
    const response: FormDefinition = await getActiveFormDefinition(formId, true);
    formJson = response?.jsonData ? response.jsonData : null;
  } catch (error) {
    console.error("Failed to load form:", error);
    formJson = null;
  }

  return formJson;
}

const getPartialSubmission = async (formId: string, token: string, cookieStore: ReadonlyRequestCookies): Promise<Submission | null> => {
  try {
    const submission = await getExistingSubmission(formId, token);
    return submission;
  } catch (error) {
    console.error("Failed to load submission:", error);
    deleteTokenFromCookie(cookieStore, formId);
    return null;
  }
}

export default ShareSurveyPage;
