"use server";

import { changePassword } from "@/services/api";
import { z } from "zod";

export interface ChangePasswordState {
  success: boolean;
  errors?: FieldErrors;
  errorMessage?: string;
}

interface FieldErrors {
  currentPassword?: string[];
  newPassword?: string[];
  confirmPassword?: string[];
}

const PasswordFormSchema = z
  .object({
    currentPassword: z
      .string()
      .min(8, { message: "Password must be at least 8 characters" }),
    newPassword: z
      .string()
      .min(8, { message: "Password must be at least 8 characters" }),
    confirmPassword: z.string(),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    path: ["confirmPassword"],
    message: "Passwords do not match",
  });

export const changePasswordAction = async (
  prevState: ChangePasswordState,
  formData: FormData,
): Promise<ChangePasswordState> => {
  const validatedFields = PasswordFormSchema.safeParse({
    currentPassword: formData.get("currentPassword"),
    newPassword: formData.get("newPassword"),
    confirmPassword: formData.get("confirmPassword"),
  });

  if (!validatedFields.success) {
    return {
      success: false,
      errors: validatedFields.error.flatten().fieldErrors,
      errorMessage:
        "Could not change password. Please check your input and try again.",
    };
  }

  const { currentPassword, newPassword, confirmPassword } =
    validatedFields.data;

  try {
    await changePassword(currentPassword, newPassword, confirmPassword);
    return {
      success: true,
      errors: {},
      errorMessage: "",
    };
  } catch (error) {
    return {
      success: false,
      errorMessage: "Failed to change password. Error: " + error,
    };
  }
};
