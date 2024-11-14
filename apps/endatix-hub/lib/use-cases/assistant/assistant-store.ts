import { ChatContext } from "./types";
import { SurveyModel } from "./survey";

const KEYS = {
    CHAT_CONTEXT: 'edx_context_chat',
    FORM_JSON: 'edx_context_form'
};

export class AssistantStore {
    private storage: Storage;

    constructor() {
        this.storage = localStorage;
    }

    public getChatContext(): ChatContext {
        return this.getItem<ChatContext>(KEYS.CHAT_CONTEXT);
    }

    public setChatContext(context: ChatContext): void {
        this.storeItem(KEYS.CHAT_CONTEXT, context);
    }

    public getFormModel(): SurveyModel | null {
        return this.getItem(KEYS.FORM_JSON);
    }

    public setFormModel(form: string): void {
        this.storeItem<string>(KEYS.FORM_JSON, form);
    }

    public clear(): void {
        this.removeItem(KEYS.CHAT_CONTEXT);
        this.removeItem(KEYS.FORM_JSON);
    }

    private storeItem<T>(key: string, value: T): void {
        this.storage.setItem(key, JSON.stringify(value));
    }

    private getItem<T>(key: string): T {
        const item = this.storage.getItem(key);
        return item ? JSON.parse(item) : null as T;
    }

    private removeItem(key: string): void {
        this.storage.removeItem(key);
    }
}
