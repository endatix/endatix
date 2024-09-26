import { AuthenticationRequest, AuthenticationResponse } from "@/lib/authDefinitions";
import { Form, FormDefinition, Submission } from "../types";
import { getSession } from "@/lib/auth-service";

const API_BASE_URL = `${process.env.ENDATIX_BASE_URL}/api`;

export const getForms = async (): Promise<Form[]> => {
  let session = await getSession();

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

export const getFormById = async (formId: string): Promise<Form> => {
    let session = await getSession();

    const response = await fetch(`${API_BASE_URL}/forms/${formId}`, {
      headers: {
        Authorization: `Bearer ${session?.token}`,
      },
    });

    if (!response.ok) {
      throw new Error("Failed to fetch form");
    }

    return response.json();
};


export const updateForm = async (formId: string, data: { name?: string, isEnabled?: boolean }): Promise<void> => {
    let session = await getSession();

    const response = await fetch(`${API_BASE_URL}/forms/${formId}`, {
      method: "PATCH",
      headers: {
        Authorization: `Bearer ${session?.token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      throw new Error("Failed to update form");
    }
};

export const updateFormDefinition = async (formId: string, jsonData: string): Promise<void> => {
    let session = await getSession();

    const response = await fetch(`${API_BASE_URL}/forms/${formId}/definition`, {
      method: "PATCH",
      headers: {
        Authorization: `Bearer ${session?.token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ jsonData }),
    });

    if (!response.ok) {
      throw new Error("Failed to update form definition");
    }
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
