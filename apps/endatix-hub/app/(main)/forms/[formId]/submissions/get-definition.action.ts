'use server'

import { ensureAuthenticated } from '@/lib/auth-service';
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
    await ensureAuthenticated();
    
    const resultState: SelectedDefinitionResult = {
        isSuccess: false
    }

    if (!definitionId) {
        resultState.errors = ["Definition ID is required"];
        return resultState;
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