import {
  getSession,
  AuthenticationRequest,
  AuthenticationResponse,
} from "@/features/auth";
import { CreateFormRequest } from "@/lib/form-types";
import { Form, FormDefinition, Submission } from "../types";
import { redirect } from "next/navigation";
import { HeaderBuilder } from "./header-builder";
import { SubmissionData } from "@/features/public-form/application/actions/submit-form.action";

const API_BASE_URL = `${process.env.ENDATIX_BASE_URL}/api`;

export const authenticate = async (
  request: AuthenticationRequest,
): Promise<AuthenticationResponse> => {
  const headers = new HeaderBuilder().acceptJson().provideJson().build();

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

export const createForm = async (
  formRequest: CreateFormRequest,
): Promise<Form> => {
  const session = await getSession();
  const headers = new HeaderBuilder()
    .withAuth(session)
    .acceptJson()
    .provideJson()
    .build();

  const response = await fetch(`${API_BASE_URL}/forms`, {
    method: "POST",
    headers: headers,
    body: JSON.stringify(formRequest),
  });

  if (!response.ok) {
    throw new Error("Failed to create form");
  }

  return response.json();
};

export const getForms = async (): Promise<Form[]> => {
  const session = await getSession();
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
    requestOptions,
  );

  if (!response.ok) {
    throw new Error("Failed to fetch form");
  }

  return response.json();
};

export const updateForm = async (
  formId: string,
  data: { name?: string; isEnabled?: boolean },
): Promise<void> => {
  const session = await getSession();
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

export const deleteForm = async (formId: string): Promise<string> => {
  const session = await getSession();

  if (!session.isLoggedIn) {
    redirect("/login");
  }

  const headers = new HeaderBuilder().withAuth(session).build();

  const response = await fetch(`${API_BASE_URL}/forms/${formId}`, {
    method: "DELETE",
    headers: headers,
  });

  if (!response.ok) {
    throw new Error("Failed to delete form");
  }

  return response.text();
};

export const getActiveFormDefinition = async (
  formId: string,
  allowAnonymous: boolean = false,
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
    requestOptions,
  );

  if (!response.ok) {
    throw new Error(`Failed to fetch form definition for formId ${formId}`);
  }

  return response.json();
};

export const getFormDefinition = async (
  formId: string,
  definitionId: string,
): Promise<FormDefinition> => {
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

  const headers = new HeaderBuilder().withAuth(session).build();

  requestOptions.headers = headers;

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/definitions/${definitionId}`,
    requestOptions,
  );

  if (!response.ok) {
    throw new Error("Failed to fetch form definition");
  }

  return response.json();
};

export const updateFormDefinition = async (
  formId: string,
  isDraft: boolean,
  jsonData: string,
): Promise<void> => {
  const session = await getSession();

  if (!session.isLoggedIn) {
    redirect("/login");
  }

  const headers = new HeaderBuilder()
    .withAuth(session)
    .acceptJson()
    .provideJson()
    .build();

  const response = await fetch(`${API_BASE_URL}/forms/${formId}/definition`, {
    method: "PATCH",
    headers: headers,
    body: JSON.stringify({ isDraft, jsonData }),
  });

  if (!response.ok) {
    throw new Error("Failed to update form definition");
  }
};

export const getSubmissions = async (formId: string): Promise<Submission[]> => {
  const session = await getSession();
  if (!session.isLoggedIn) {
    redirect("/login");
  }

  const CLIENT_PAGE_SIZE = 10_000;
  const headers = new HeaderBuilder().withAuth(session).build();

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/submissions?pageSize=${CLIENT_PAGE_SIZE}`,
    {
      headers: headers,
    },
  );

  if (!response.ok) {
    throw new Error("Failed to fetch data");
  }

  return response.json();
};

export const createSubmissionPublic = async (
  formId: string,
  submissionData: SubmissionData,
): Promise<Submission> => {
  if (!formId) {
    throw new Error("FormId is required");
  }

  const headers = new HeaderBuilder().acceptJson().provideJson().build();

  const requestOptions: RequestInit = {
    method: "POST",
    headers: headers,
    body: JSON.stringify(submissionData),
  };

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/submissions`,
    requestOptions,
  );

  if (!response.ok) {
    throw new Error("Failed to submit response");
  }

  return response.json();
};

export const updateSubmissionPublic = async (
  formId: string,
  token: string,
  submissionData: SubmissionData,
): Promise<Submission> => {
  if (!formId || !token) {
    throw new Error("FormId or token is required");
  }

  const headers = new HeaderBuilder().acceptJson().provideJson().build();

  const requestOptions: RequestInit = {
    method: "PATCH",
    headers: headers,
    body: JSON.stringify(submissionData),
  };

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/submissions/by-token/${token}`,
    requestOptions,
  );

  if (!response.ok) {
    throw new Error("Failed to submit response");
  }

  return response.json();
};

export const updateSubmission = async (
  formId: string,
  submissionId: string,
  submissionData: SubmissionData,
): Promise<Submission> => {
  const session = await getSession();

  if (!session.isLoggedIn) {
    redirect("/login");
  }

  if (!formId || !submissionId) {
    throw new Error("FormId or submissionId is required");
  }

  const headers = new HeaderBuilder()
    .withAuth(session)
    .acceptJson()
    .provideJson()
    .build();

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/submissions/${submissionId}`,
    {
      method: "PATCH",
      headers: headers,
      body: JSON.stringify(submissionData),
    },
  );

  if (!response.ok) {
    throw new Error("Failed to update submission");
  }

  return response.json();
};

interface UpdateSubmissionStatusRequest {
  status: string;
  formId: string;
  dateUpdated: Date;
}

export const updateSubmissionStatus = async (
  formId: string,
  submissionId: string,
  status: string,
): Promise<UpdateSubmissionStatusRequest> => {
  const session = await getSession();

  if (!session.isLoggedIn) {
    redirect("/login");
  }

  const headers = new HeaderBuilder()
    .withAuth(session)
    .acceptJson()
    .provideJson()
    .build();

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/submissions/${submissionId}/status`,
    {
      method: "POST",
      headers: headers,
      body: JSON.stringify({ status }),
    },
  );
  console.log("Status", status);
  if (!response.ok) {
    throw new Error("Failed to change submission status");
  }

  return response.json();
};

export const getPartialSubmissionPublic = async (
  formId: string,
  token: string,
): Promise<Submission> => {
  if (!formId || !token) {
    throw new Error("FormId or token is required");
  }

  const headers = new HeaderBuilder().acceptJson().build();

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/submissions/by-token/${token}`,
    {
      headers: headers,
    },
  );

  if (!response.ok) {
    throw new Error("Failed to fetch submission");
  }

  return response.json();
};

export const getSubmission = async (
  formId: string,
  submissionId: string,
): Promise<Submission> => {
  if (!formId || !submissionId) {
    throw new Error("FormId or submissionId is required");
  }

  const session = await getSession();

  if (!session.isLoggedIn) {
    redirect("/login");
  }

  const headers = new HeaderBuilder().withAuth(session).acceptJson().build();

  const response = await fetch(
    `${API_BASE_URL}/forms/${formId}/submissions/${submissionId}`,
    {
      headers: headers,
    },
  );

  if (!response.ok) {
    throw new Error("Failed to fetch submission");
  }

  return response.json();
};

export const changePassword = async (
  currentPassword: string,
  newPassword: string,
  confirmPassword: string,
): Promise<string> => {
  if (!currentPassword || !newPassword || !confirmPassword) {
    throw new Error(
      "Current password, new password or confirm password is required",
    );
  }

  const session = await getSession();

  if (!session.isLoggedIn) {
    redirect("/login");
  }

  const headers = new HeaderBuilder()
    .withAuth(session)
    .acceptJson()
    .provideJson()
    .build();

  const response = await fetch(`${API_BASE_URL}/my-account/change-password`, {
    method: "POST",
    headers: headers,
    body: JSON.stringify({ currentPassword, newPassword, confirmPassword }),
  });

  if (!response.ok) {
    throw new Error("Failed to change password");
  }

  return response.json();
};
