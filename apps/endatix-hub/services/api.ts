import { AuthenticationRequest, AuthenticationResponse } from "@/lib/authDefinitions";
import { Form, FormDefinition, Submission } from "../types";
import { getSession } from "@/lib/auth-service";
import { redirect } from "next/navigation";

const API_BASE_URL = `${process.env.ENDATIX_BASE_URL}/api`;

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

export const getForm = async (formId: string): Promise<Form> => {
  const requestOptions: RequestInit = {};

  const session = await getSession();

  if (!session.isLoggedIn) {
    redirect("/login");
  }

  requestOptions.headers = {
    Authorization: `Bearer ${session?.token}`,
  }

  const response = await fetch(`${API_BASE_URL}/forms/${formId}`, requestOptions);

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

export const getFormDefinition = async (formId: string, allowAnonymous: boolean = false): Promise<FormDefinition> => {
  const requestOptions: RequestInit = {};
  if (allowAnonymous) {
    const session = await getSession();

    if (!session.isLoggedIn) {
      redirect("/login");
    }

    requestOptions.headers = {
      Authorization: `Bearer ${session?.token}`,
    }
  }

  const response = await fetch(`${API_BASE_URL}/forms/${formId}/definition`, requestOptions);

  if (!response.ok) {
    throw new Error(`Failed to fetch form definition for formId ${formId}`);
  }

  return response.json();
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

export const getSubmissions = async (formId: string): Promise<Submission[]> => {
  let session = await getSession();
  if (!session.isLoggedIn) {
    redirect("/login");
  }

  const response = await fetch(`${API_BASE_URL}/forms/${formId}/submissions`, {
    headers: {
      Authorization: `Bearer ${session.token}`
    }
  });

  if (!response.ok) {
    throw new Error("Failed to fetch data");
  }
  return response.json();
};

export const sendSubmission = async (formId: string, submissionData: any): Promise<Submission> => {
  const requestOptions: RequestInit = {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify(submissionData),
  };

  const response = await fetch(`${API_BASE_URL}/forms/${formId}/submissions`, requestOptions);

  if (!response.ok) {
    throw new Error("Failed to submit response");
  }

  return response.json();
};