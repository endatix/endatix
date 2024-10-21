"use server"

import { defineForm } from "@/services/api";
import { PromptResult, IPromptResult } from "./prompt-result";

export async function defineFormAction(
  prevState: IPromptResult,
  formData: FormData
): Promise<IPromptResult> {
  const prompt = formData.get("prompt");

  try {
    const response = await defineForm({ prompt: prompt as string });

    return PromptResult.Success(response);
  } catch (error) {
    return PromptResult.Error("Failed to process your prompt. Please try again and if the issue persists, contact support.");
  }
}