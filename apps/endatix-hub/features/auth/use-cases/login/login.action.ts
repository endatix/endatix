"use server";

import { AuthService } from "@/features/auth";
import { authenticate } from "@/services/api";
import { AuthenticationRequestSchema } from "../../shared/auth.schemas";
import { AuthenticationRequest } from "../../shared/auth.types";

const CONNECTION_REFUSED_CODE = "ECONNREFUSED";

interface LoginActionState {
  success: boolean;
  errors?: FieldErrors;
  errorMessage?: string;
}

interface FieldErrors {
  email?: string[];
  password?: string[];
}

export async function loginAction(
  _: unknown,
  formData: FormData,
): Promise<LoginActionState> {
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
    const { email, accessToken, refreshToken } = authenticationResponse;

    const authService = new AuthService();
    await authService.login(accessToken, refreshToken, email);
  } catch (error: unknown) {
    let errorMessage =
      "We cannot log you in at this time. Please check your credentials and try again";
    if (
      error instanceof Error &&
      error?.cause &&
      typeof error.cause === "object" &&
      "code" in error.cause &&
      error.cause.code == CONNECTION_REFUSED_CODE
    ) {
      errorMessage =
        "Failed to connect to the Endatix API. Ensure your network connection and app settings are correct";
    }

    return {
      success: false,
      errorMessage: errorMessage,
    } as LoginActionState;
  }

  return { success: true };
}
