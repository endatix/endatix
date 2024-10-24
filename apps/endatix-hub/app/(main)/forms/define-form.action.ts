"use server"

import { defineForm } from "@/services/api";
import { PromptResult, IPromptResult } from "./prompt-result";
import { Model } from "survey-core";
import { DefineFormRequest } from "@/lib/use-cases/assistant";

export async function defineFormAction(
  prevState: IPromptResult,
  formData: FormData
): Promise<IPromptResult> {
  const prompt = formData.get("prompt");
  const threadId = formData.get("threadId");
  const assistantId = formData.get("assistantId");

  try {
    let request = {
      prompt: prompt as string
    } as DefineFormRequest;

    if (threadId && assistantId) {
        request.threadId = threadId as string,
        request.assistantId = assistantId as string
    }

    const response = await defineForm(request);

    var validatedModel = new Model(response.definition);
    response.definition = validatedModel.toJSON()

    return PromptResult.Success(response);
  } catch (error) {
    return PromptResult.Error("Failed to process your prompt. Please try again and if the issue persists, contact support.");
  }
}