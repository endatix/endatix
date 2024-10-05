import { FormDefinition } from '@/types';
import dynamic from 'next/dynamic';
import { getFormDefinitionByFormId } from "@/services/api";

const SurveyComponent = dynamic(() => import('@/components/survey'), {
  ssr: false,
});


export default async function Survey({ params }: { params: { formId: string } }) {
  const surveyJson = await getServerSideProps(params.formId);

  if(!surveyJson) {
    return <div>Form not found</div>;
  }

  return (
    <div className="flex min-h-screen flex-col items-center p-8">
      <SurveyComponent definition={surveyJson} formId={params.formId} />
    </div>
  );
}

const getServerSideProps = async (formId: string) => {

  let formJson: string | null = null;

  try {
    const response: FormDefinition = await getFormDefinitionByFormId(formId);
    formJson = response?.jsonData ? response.jsonData : null;
  } catch (error) {
    console.error("Failed to load form:", error);
    formJson = null;
  }

  return formJson;
}
