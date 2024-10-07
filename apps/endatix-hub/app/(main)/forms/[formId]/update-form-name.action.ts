"use server";

import { updateForm  } from "@/services/api";

export async function updateFormNameAction(formId: string, formName: string) {
  try {
    await updateForm (formId, { name: formName });
    return { success: true };
  } catch (error) {
    console.error("Failed to update form name", error);
    return { success: false, error: "Failed to update form name" };
  }
}
