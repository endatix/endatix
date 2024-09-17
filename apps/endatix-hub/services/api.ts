import { Form, Submission } from "../types";

const API_BASE_URL = `${process.env.ENDATIX_BASE_URL}/api`;

export const getForms = async (): Promise<Form[]> => {
  const response = await fetch(`${API_BASE_URL}/forms`);
  if (!response.ok) {
    throw new Error("Failed to fetch data");
  }
  return response.json();
};

export const getSubmissionsByFormId = async (formId: string): Promise<Submission[]> => {
  const response = await fetch(`${API_BASE_URL}/forms/${formId}/submissions`);
  if (!response.ok) {
    throw new Error('Failed to fetch data');
  }
  return response.json();
};
