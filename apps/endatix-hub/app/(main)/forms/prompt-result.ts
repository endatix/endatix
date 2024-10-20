import { DefineFormResponse } from "@/services/api";

export interface IPromptResult {
    success?: boolean;
    errorMessage?: string;
    value?: DefineFormResponse;
}

export class PromptResult implements IPromptResult {
    success?: boolean;
    errorMessage?: string;
    value?: DefineFormResponse;

    private constructor(success?: boolean, errorMessage?: string, value?: DefineFormResponse) {
        this.success = success;
        this.errorMessage = errorMessage;
        this.value = value;
    }

    isError = (): boolean => this.success === false && this.errorMessage !== undefined;

    static Success(value: DefineFormResponse): IPromptResult {
        return {
            success: true,
            value: value
        }
    }

    static Error(errorMessage: string): IPromptResult {
        return {
            success: false,
            errorMessage: errorMessage
        }
    }

    static InitialState(): IPromptResult {
        return {}
    }
}
