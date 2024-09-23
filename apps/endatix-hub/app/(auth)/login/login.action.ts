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

export async function loginAction(prevState: any, queryData: any): Promise<LoginActionState> {
  const email = queryData.get("email");
  const password = queryData.get("password");

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

  var authRequest: AuthenticationRequest = {
    email: email,
    password: password,
  };

  try {
    const authenticationResponse = await authenticate(authRequest);
    const { email, token } = authenticationResponse;
    await login(token, email);
  } catch (error: any) {
    let errorMessage = "We cannot log you in at this time. Please check your credentials and try again";
    if (error?.cause?.code == CONNECTION_REFUSED_CODE) {
      errorMessage = "Cannot connect to the Endatix API. Please check your settings and restart the Endatix Hub application";
    }

    return {
      success: false,
      errorMessage: errorMessage
    } as LoginActionState;
  }

  redirect("/");
}