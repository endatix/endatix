"use server";

import { login } from "@/lib/auth-service";
import {
  AuthenticationRequest,
  AuthenticationRequestSchema,
} from "@/lib/auth-definitions";
import { authenticate } from "@/services/api";
import { redirect } from 'next/navigation'

const CONNECTION_REFUSED_CODE = "ECONNREFUSED";

interface LoginActionState {
  success: boolean,
  errors?: FieldErrors,
  errorMessage?: string
}

interface FieldErrors {
  email?: string[],
  password?: string[]
}

export async function loginAction(prevState: unknown, formData: FormData): Promise<LoginActionState> {
  console.log(`prevState is ${prevState}`);
  const email = formData.get("email");
  const password = formData.get("password");

  const validatedFields = AuthenticationRequestSchema.safeParse({
    email: email,
    password: password,
  });

  if (!validatedFields.success) {
    return {
      success: false,
      errors: validatedFields.error.flatten().fieldErrors,
    } as LoginActionState;
  }

  const data = validatedFields.data;
  const authRequest: AuthenticationRequest = {
    email: data.email,
    password: data.password,
  };

  try {
    const authenticationResponse = await authenticate(authRequest);
    const { email, token } = authenticationResponse;
    await login(token, email);
  } catch (error: unknown) {
    let errorMessage = "We cannot log you in at this time. Please check your credentials and try again";
    if (error instanceof Error && error?.cause && typeof error.cause === 'object' && 'code' in error.cause && error.cause.code == CONNECTION_REFUSED_CODE) {
      errorMessage = "Cannot connect to the Endatix API. Please check your settings and restart the Endatix Hub application";
    }

    return {
      success: false,
      errorMessage: errorMessage
    } as LoginActionState;
  }

  redirect("/");
}