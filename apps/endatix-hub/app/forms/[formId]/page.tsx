import dynamic from "next/dynamic";
import { Form, FormDefinition } from "../../../types";
import { getFormById, getFormDefinitionByFormId } from "@/services/api";
import { SurveyCreatorProps } from "@/components/survey-creator";

const SurveyCreatorComponent = dynamic(() => import('@/components/survey-creator'), {
  ssr: false,
});

interface FormCreatorPageProps {
  params: {
    formId: string;
  };
}

const FormCreatorPage = async ({ params }: FormCreatorPageProps) => {
  const { formId } = params;

  let form: Form | null = null;
  let formJson: object | null = null;

  try {
    form = await getFormById(formId);

    const response: FormDefinition = await getFormDefinitionByFormId(formId);
    formJson = response?.jsonData ? JSON.parse(response.jsonData) : null;
  } catch (error) {
    console.error("Failed to load form:", error);
    formJson = null;
  }

  if (!form || !formJson) {
    return <div>Form not found</div>;
  }

  const props: SurveyCreatorProps = {
    formId: formId,
    formJson: formJson,
    formName: form.name,
    formIdLabel: form.id,
    isEnabled: form.isEnabled
  };

  return (
    <div className="flex min-h-screen flex-col items-center p-8">
      <SurveyCreatorComponent {...props} />
    </div>
  );
}

export default FormCreatorPage
