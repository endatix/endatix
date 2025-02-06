"use server";

import { revalidatePath } from "next/cache";
import { changeStatusUseCase } from "./change-status.use-case";
import { ChangeStatusCommand, ChangeStatusResult } from "./types";

export async function changeStatusAction(
  command: ChangeStatusCommand,
): Promise<ChangeStatusResult> {
  const success = await changeStatusUseCase(command);

  if (success) {
    revalidatePath(
      `/forms/${command.formId}/submissions/${command.submissionId}`,
    );
    return { success: true };
  }

  return {
    success: false,
    error: "Failed to update submission status. Please try again.",
  };
}
