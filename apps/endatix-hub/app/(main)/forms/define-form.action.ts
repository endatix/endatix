"use server"

import { defineForm } from "@/services/api";
import { DefineFormResult, IDefineFormResult } from "./define-form-result";

export async function defineFormAction(
  prevState: IDefineFormResult,
  formData: FormData
): Promise<IDefineFormResult> {
  console.log(`prevState is ${JSON.stringify(prevState)}`);
  const prompt = formData.get("prompt");

  try {
    const response = await defineForm({ prompt: prompt as string });

    return DefineFormResult.Success(response) as IDefineFormResult;
  } catch (error) {
    return {
      success: false,
      errorMessage: "Failed to process your prompt. Please try again and if the issue persists, contact support."
    };
  }
}