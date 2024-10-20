import { DefineFormResponse } from "@/services/api";

export interface IDefineFormResult {
    success?: boolean;
    errorMessage?: string;
    value?: DefineFormResponse;
}

export class DefineFormResult implements IDefineFormResult {
    success?: boolean;
    errorMessage?: string;
    value?: DefineFormResponse;

    private constructor(success?: boolean, errorMessage?: string, value?: DefineFormResponse) {
        this.success = success;
        this.errorMessage = errorMessage;
        this.value = value;
    }

    isError = (): boolean => this.success === false && this.errorMessage !== undefined;

    static Success(value: DefineFormResponse): IDefineFormResult {
        return {
            success: true,
            value: value
        }
    }

    static Error(errorMessage: string): IDefineFormResult {
        return {
            success: false,
            errorMessage: errorMessage
        }
    }

    static InitialState(): IDefineFormResult {
        return {}
    }
}
