'use server'

import { ensureAuthenticated } from "@/lib/auth-service";
import { CreateFormRequest, CreateFormResult } from "@/lib/form-types";
import { createForm } from "@/services/api";

export async function createFormAction(request: CreateFormRequest): Promise<CreateFormResult> {
  await ensureAuthenticated();

  const result: CreateFormResult = {
    isSuccess: false
  }

  try {
    const form = await createForm(request);
    if (form.id?.length > 0) {
      result.formId = form.id;
      result.isSuccess = true;
    } else {
      result.error = "Failed to create form";
    }
  } catch (error) {
    console.error("Failed to create form", error);
  } finally {
    return result;
  }
}
