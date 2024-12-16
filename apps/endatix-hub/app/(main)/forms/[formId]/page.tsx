import { Form, FormDefinition } from "../../../../types";
import { getForm, getActiveFormDefinition } from "@/services/api";
import { FormEditorProps } from "./ui/form-editor";
import FormEditorContainer from "./ui/form-editor-container";

type Params = {
  params: Promise<{ formId: string }>
};

async function FormEditPage({ params }: Params) {
  const formId = (await params).formId;

  let form: Form | null = null;
  let formJson: object | null = null;

  try {
    form = await getForm(formId);

    const response: FormDefinition = await getActiveFormDefinition(formId);
    formJson = response?.jsonData ? JSON.parse(response.jsonData) : null;
  } catch (error) {
    console.error("Failed to load form:", error);
    formJson = null;
  }

  if (!form || !formJson) {
    return <div>Form not found</div>;
  }

  const props: FormEditorProps = {
    formId: formId,
    formJson: formJson,
    formName: form.name
  };

  return (
    <div className="h-dvh overflow-hidden gap-4">
      <FormEditorContainer {...props} />
    </div>
  );
}

export default FormEditPage;
