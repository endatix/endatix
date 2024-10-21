import { DefineFormContext } from "@/lib/use-cases/assistant";

export interface IPromptResult {
    success?: boolean;
    errorMessage?: string;
    value?: DefineFormContext;
}

export class PromptResult implements IPromptResult {
    success?: boolean;
    errorMessage?: string;
    value?: DefineFormContext;

    private constructor(success?: boolean, errorMessage?: string, value?: DefineFormContext) {
        this.success = success;
        this.errorMessage = errorMessage;
        this.value = value;
    }

    isError = (): boolean => this.success === false && this.errorMessage !== undefined;

    static Success(value: DefineFormContext): IPromptResult {
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
