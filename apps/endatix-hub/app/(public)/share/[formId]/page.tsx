"use server"

import { FormDefinition } from '@/types';
import { getActiveFormDefinition } from "@/services/api";
import SurveyJsContainer from './ui/survey-js-container';

type ShareSurveyPageProps = {
  params: Promise<{ formId: string }>
};


async function ShareSurveyPage({ params }: ShareSurveyPageProps) {
  const { formId } = await params;
  const surveyJson = await getSurveyJson(formId);

  if (!surveyJson) {
    return <div>Form not found</div>;
  }

  return (
    <div className="flex min-h-screen flex-col items-center p-8">
      <SurveyJsContainer formId={formId} definition={surveyJson} />
    </div>
  );
}

const getSurveyJson = async (formId: string) => {
  let formJson: string | null = null;

  try {
    const response: FormDefinition = await getActiveFormDefinition(formId);
    formJson = response?.jsonData ? response.jsonData : null;
  } catch (error) {
    console.error("Failed to load form:", error);
    formJson = null;
  }

  return formJson;
}

export default ShareSurveyPage;
