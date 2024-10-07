"use server";

import { getFormDefinition } from '@/services/api';

export interface GetDefinitionRequest {
    formId: string,
    definitionId?: string
}

export interface SelectedDefinitionResult {
    isSuccess: boolean,
    errors?: string[],
    definitionsData?: string
}

export async function getDefinition({ formId, definitionId }: GetDefinitionRequest): Promise<SelectedDefinitionResult> {
    const resultState: SelectedDefinitionResult = {
        isSuccess: false
    }

    try {
        const formDefinition = await getFormDefinition(formId, definitionId);
        resultState.definitionsData = formDefinition?.jsonData;
        resultState.isSuccess = true;
    } catch {
        resultState.errors = ["this is not good"];
    } finally {
        return resultState;
    }
}