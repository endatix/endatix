"use server";

import { ensureAuthenticated } from "@/lib/auth-service";
import { CreateFormRequest } from "@/lib/form-types";
import { Result } from "@/lib/result";
import { createForm } from "@/services/api";

export type CreateFormResult = Result<string>;

export async function createFormAction(
  request: CreateFormRequest,
): Promise<CreateFormResult> {
  await ensureAuthenticated();

  try {
    const form = await createForm(request);
    if (form.id?.length > 0) {
      return Result.success(form.id);
    }
  } catch (error) {
    console.error("Failed to create form", error);
  }
  return Result.error("Failed to create form");
}
