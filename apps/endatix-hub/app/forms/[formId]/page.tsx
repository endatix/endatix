import dynamic from "next/dynamic";
import { Form, FormDefinition } from "../../../types";
import { getFormById, getFormDefinitionByFormId } from "@/services/api";

const SurveyCreatorComponent = dynamic(() => import('@/components/survey-creator'), {
  ssr: false,
});

export default async function FormCreator({ params }: { params: { formId: string } }) {
  const { formId } = params;

  let form: Form | null = null;
  let formJson: Object | null = null;

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

  return (
    <div className="flex min-h-screen flex-col items-center p-8">
      <SurveyCreatorComponent
        formId={formId}
        formJson={formJson}
        formName={form.name}
        formIdLabel={form.id}
        isEnabled={form.isEnabled}
      />
    </div>
  );
}
