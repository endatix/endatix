import { AuthenticationRequest, AuthenticationResponse } from "@/lib/auth-definitions";
import { Form, Submission } from "../types";
import { getSession } from "@/lib/auth-service";

const API_BASE_URL = `${process.env.ENDATIX_BASE_URL}/api`;

export const getForms = async (): Promise<Form[]> => {
  const currentSession = await getSession();

  const response = await fetch(`${API_BASE_URL}/forms`, {
    headers: {
      Authorization: `Bearer ${currentSession.token}`
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