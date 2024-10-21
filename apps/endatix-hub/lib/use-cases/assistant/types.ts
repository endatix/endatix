export type Message = {
    isAi: boolean;
    content: string;
}

export type ChatContext = {
    assistantId?: string,
    threadId: string
    messages: Message[];
};

export interface DefineFormRequest {
    prompt: string;
    definition?: string,
    assistantId?: string,
    threadId?: string
}

export interface DefineFormContext {
    definition?: string,
    assistantId?: string,
    threadId: string
}