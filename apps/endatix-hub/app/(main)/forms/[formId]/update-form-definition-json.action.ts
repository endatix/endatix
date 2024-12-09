'use server'

import { ensureAuthenticated } from "@/lib/auth-service";
import { updateFormDefinition } from "@/services/api";

export async function updateFormDefinitionJsonAction(formId: string, formJson: object | null) {
  await ensureAuthenticated();

  try {
    await updateFormDefinition(formId, JSON.stringify(formJson));
    return { success: true };
  } catch (error) {
    console.error("Failed to update form definition", error);
    return { success: false, error: "Failed to update form definition" };
  }
}
