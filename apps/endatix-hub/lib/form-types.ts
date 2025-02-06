export interface CreateFormRequest {
  name: string;
  isEnabled: boolean;
  formDefinitionJsonData: string;
}

export interface CreateFormResult {
  isSuccess: boolean;
  error?: string;
  formId?: string;
}
