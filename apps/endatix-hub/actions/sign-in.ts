"use server";

import {
  AuthenticationRequest,
  AuthenticationRequestSchema,
} from "@/lib/authDefinitions";
import { authenticate } from "@/services/api";
import { cookies } from "next/headers";

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

  } catch {

    return {
      success: false,
      errors: {
        primary:
          "We cannot complete your request at this time. Please try again later",
      },
    };
  }
}
