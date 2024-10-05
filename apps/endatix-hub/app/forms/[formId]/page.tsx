import { Form, FormDefinition } from "../../../types";
import { getForm, getFormDefinition } from "@/services/api";
import FormEditor from "./ui/form-editor";

interface FormCreatorPageProps {
  params: {
    formId: string;
  };
}

const FormCreatorPage = async ({ params }: FormCreatorPageProps) => {
  const { formId } = await params;

  let form: Form | null = null;
  let formJson: object | null = null;

  try {
    form = await getForm(formId);

    const response: FormDefinition = await getFormDefinition(formId);
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
      <FormEditor {...props} />
    </div>
  );
}

export default FormCreatorPage
