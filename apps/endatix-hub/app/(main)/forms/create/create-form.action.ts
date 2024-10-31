"use server"

import { CreateFormRequest } from "@/lib/use-cases/assistant";
import { createForm } from "@/services/api";

export interface CreateFormDraftResult {
    isSuccess: boolean,
    error?: string,
    formId?: string
}

export async function createFormDraft(request: CreateFormRequest): Promise<CreateFormDraftResult> {
    const result: CreateFormDraftResult = {
        isSuccess: false
    }

    if (!request) {
        result.error = "Request is null";
        return result;
    }

    try {
        const formDraft = await createForm(request);
        if (formDraft.id?.length > 0) {
            result.formId = formDraft.id;
            result.isSuccess = true;
        } else {
            result.error = "Failed to create form draft";
        }
    } catch (er) {
        result.error = `Failed to create form draft. Details: ${er}`;
    } finally {
        return result;
    }
}