"use server";

import { updateForm  } from "@/services/api";

export async function updateFormStatusAction(formId: string, isEnabled: boolean) {
  try {
    await updateForm (formId, { isEnabled });
    return { success: true };
  } catch (error) {
    console.error("Failed to update form status", error);
    return { success: false, error: "Failed to update form status" };
  }
}
