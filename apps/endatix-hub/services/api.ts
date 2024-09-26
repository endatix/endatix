import { AuthenticationRequest, AuthenticationResponse } from "@/lib/authDefinitions";
import { Form, FormDefinition, Submission } from "../types";
import { getSession } from "@/lib/auth-service";

const API_BASE_URL = `${process.env.ENDATIX_BASE_URL}/api`;

export const getForms = async (): Promise<Form[]> => {
  let session = await getSession();
  debugger

  const response = await fetch(`${API_BASE_URL}/forms`, {
    headers: {
      Authorization: `Bearer ${session.token}`
    }
  });

  if (!response.ok) {
    throw new Error("Failed to fetch data");
  }

  return response.json();
};

export const getSubmissionsByFormId = async (
  formId: string
): Promise<Submission[]> => {
  const response = await fetch(`${API_BASE_URL}/forms/${formId}/submissions`);
  if (!response.ok) {
    throw new Error("Failed to fetch data");
  }
  return response.json();
};

export const authenticate = async (
  request: AuthenticationRequest
): Promise<AuthenticationResponse> => {
  const response = await fetch(`${API_BASE_URL}/auth/login`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new Error("Failed to fetch data");
  }

  return response.json();
};

export const getFormDefinitionByFormId = async (formId: string): Promise<FormDefinition> => {
  let session = await getSession();

  const response = await fetch(`${API_BASE_URL}/forms/${formId}/definition`, {
    headers: {
      Authorization: `Bearer ${session?.token}`
    }
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch form definition for formId ${formId}`);
  }

  return response.json();
};

export const sendSubmission = async (
  formId: string,
  submissionData: any
): Promise<Submission[]> => {
  const response = await fetch(`${API_BASE_URL}/forms/${formId}/submissions`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify(submissionData),
  });
  if (!response.ok) {
    throw new Error("Failed to submit response");
  }
  return response.json();
};