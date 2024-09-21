"use server";

import { login } from "@/lib/auth-service";
import {
  AuthenticationRequest,
  AuthenticationRequestSchema,
} from "@/lib/authDefinitions";
import { authenticate } from "@/services/api";
import { redirect } from 'next/navigation'

export async function signIn(prevState: any, queryData: any) {
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
    };
  }

  var authRequest: AuthenticationRequest = {
    email: email,
    password: password,
  };

  try {
    const authenticationResponse = await authenticate(authRequest);
    const { email, token } = authenticationResponse;
    await login(token, email);
  } catch {
    return {
      success: false,
      error: "We cannot log you in at this time. Please check your credentials and try again"
    };
  }

  redirect("/");
}