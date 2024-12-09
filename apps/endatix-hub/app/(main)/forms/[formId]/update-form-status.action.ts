'use server'

import { ensureAuthenticated } from "@/lib/auth-service";
import { updateForm  } from "@/services/api";

export async function updateFormStatusAction(formId: string, isEnabled: boolean) {
  await ensureAuthenticated();

  try {
    await updateForm (formId, { isEnabled });
    return { success: true };
  } catch (error) {
    console.error("Failed to update form status", error);
    return { success: false, error: "Failed to update form status" };
  }
}
