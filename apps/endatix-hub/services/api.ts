import {
  AuthenticationRequest,
  AuthenticationResponse,
} from "@/lib/auth-definitions";
import { Form, FormDefinition, Submission } from "../types";
import { redirect } from "next/navigation";
import { getSession } from "@/lib/auth-service";
import { HeaderBuilder } from "./header-builder";
import { SubmissionData } from "@/features/public-form/application/actions/submit-form.action";

const API_BASE_URL = `${process.env.ENDATIX_BASE_URL}/api`;

export const authenticate = async (
  request: AuthenticationRequest
): Promise<AuthenticationResponse> => {
  const headers = new HeaderBuilder()
    .acceptJson()
    .provideJson()
    .build();

  const response = await fetch(`${API_BASE_URL}/auth/login`, {
    method: "POST",
    headers: headers,
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new Error("Failed to fetch data");
  }

  return response.json();
};

export const getForms = async (): Promise<Form[]> => {
  let session = await getSession();
  const headers = new HeaderBuilder().withAuth(session).build();

  const response = await fetch(`${API_BASE_URL}/forms?pageSize=100`, {
    headers: headers,
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
    Authorization: `Bearer ${session?.accessToken}`,
  };

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}`,
    requestOptions
  );

  if (!response.ok) {
    throw new Error("Failed to fetch form");
  }

  return response.json();
};

export const updateForm = async (
  formId: string,
  data: { name?: string; isEnabled?: boolean }
): Promise<void> => {
  let session = await getSession();
  const headers = new HeaderBuilder()
    .withAuth(session)
    .acceptJson()
    .provideJson()
    .build();

  const response = await fetch(`${API_BASE_URL}/forms/${formId}`, {
    method: "PATCH",
    headers: headers,
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    throw new Error("Failed to update form");
  }
};

export const getActiveFormDefinition = async (
  formId: string,
  allowAnonymous: boolean = false
): Promise<FormDefinition> => {
  const requestOptions: RequestInit = {};
  const headerBuilder = new HeaderBuilder();

  if (!allowAnonymous) {
    const session = await getSession();

    if (!session.isLoggedIn) {
      redirect("/login");
    }

    headerBuilder.withAuth(session);
  }

  requestOptions.headers = headerBuilder.build();
  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/definition`,
    requestOptions
  );

  if (!response.ok) {
    throw new Error(`Failed to fetch form definition for formId ${formId}`);
  }

  return response.json();
};

export const getFormDefinition = async (formId: string, definitionId: string): Promise<FormDefinition> => {
  if (!formId) {
    throw new Error(`FormId is required`);
  }

  if (!definitionId) {
    throw new Error(`DefinitionId is required`);
  }

  const requestOptions: RequestInit = {};
  const session = await getSession();

  if (!session.isLoggedIn) {
    redirect("/login");
  }

  const headers = new HeaderBuilder()
    .withAuth(session)
    .build();

  requestOptions.headers = headers;

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/definitions/${definitionId}`,
    requestOptions
  );

  if (!response.ok) {
    throw new Error("Failed to fetch form definition");
  }

  return response.json();
};

export const updateFormDefinition = async (
  formId: string,
  jsonData: string
): Promise<void> => {
  let session = await getSession();
  const headers = new HeaderBuilder()
    .withAuth(session)
    .acceptJson()
    .provideJson()
    .build();

  const response = await fetch(`${API_BASE_URL}/forms/${formId}/definition`, {
    method: "PATCH",
    headers: headers,
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

  const CLIENT_PAGE_SIZE = 10_000;
  const headers = new HeaderBuilder().withAuth(session).build();

  const response = await fetch(`${API_BASE_URL}/forms/${formId}/submissions?pageSize=${CLIENT_PAGE_SIZE}`, {
    headers: headers,
  });

  if (!response.ok) {
    throw new Error("Failed to fetch data");
  }

  return response.json();
};

export const createSubmission = async (
  formId: string,
  submissionData: SubmissionData
): Promise<Submission> => {
  if (!formId) {
    throw new Error("FormId is required");
  }

  const headers = new HeaderBuilder()
    .acceptJson()
    .provideJson()
    .build();

  const requestOptions: RequestInit = {
    method: "POST",
    headers: headers,
    body: JSON.stringify(submissionData),
  };

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/submissions`,
    requestOptions
  );

  if (!response.ok) {
    throw new Error("Failed to submit response");
  }

  return response.json();
};

export const updateSubmission = async (
  formId: string,
  token: string,
  submissionData: SubmissionData
): Promise<Submission> => {
  if (!formId || !token) {
    throw new Error("FormId or token is required");
  }

  const headers = new HeaderBuilder()
    .acceptJson()
    .provideJson()
    .build();

  const requestOptions: RequestInit = {
    method: "PATCH",
    headers: headers,
    body: JSON.stringify(submissionData),
  };

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/submissions/by-token/${token}`,
    requestOptions
  );

  if (!response.ok) {
    throw new Error("Failed to submit response");
  }

  return response.json();
};

export const getSubmission = async (formId: string, token: string): Promise<Submission> => {
  if (!formId || !token) {
    throw new Error("FormId or token is required");
  }

  const headers = new HeaderBuilder()
    .acceptJson()
    .build();

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/submissions/by-token/${token}`,
    { headers : headers }
  );

  if (!response.ok) {
    throw new Error("Failed to fetch submission");
  }

  return response.json();
}