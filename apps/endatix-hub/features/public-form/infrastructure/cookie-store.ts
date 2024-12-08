import { Result } from "@/lib/result";
import { ResponseCookie } from "next/dist/compiled/@edge-runtime/cookies";
import { ReadonlyRequestCookies } from "next/dist/server/web/spec-extension/adapters/request-cookies";

type FormToken = {
    formId: string;
    token: string;
}

type CookieConfig = {
    readonly name: string;
    readonly expirationInDays: number;
    readonly secure: boolean;
}

export class FormTokenCookieStore {
    private readonly COOKIE_CONFIG: CookieConfig;

    constructor(
        private readonly cookieStore: ReadonlyRequestCookies,
        config?: Partial<CookieConfig>
    ) {
        const cookieName = process.env.NEXT_FORMS_COOKIE_NAME;
        const cookieDuration = process.env.NEXT_FORMS_COOKIE_DURATION_DAYS;

        if (!cookieName) {
            throw new Error('NEXT_FORMS_COOKIE_NAME environment variable is not set');
        }

        if (!cookieDuration || isNaN(Number(cookieDuration))) {
            throw new Error('NEXT_FORMS_COOKIE_DURATION_DAYS environment variable is not set or invalid');
        }

        this.COOKIE_CONFIG = {
            name: config?.name ?? cookieName,
            expirationInDays: config?.expirationInDays ?? Number(cookieDuration),
            secure: config?.secure ?? process.env.NODE_ENV === 'production'
        };
    }

    private getExpires(): Date {
        return new Date(
            Date.now() + this.COOKIE_CONFIG.expirationInDays * 24 * 60 * 60 * 1000
        );
    }

    private getCookieOptions(): Partial<ResponseCookie> {
        return {
            httpOnly: true,
            secure: this.COOKIE_CONFIG.secure,
            sameSite: "strict",
            expires: this.getExpires(),
            path: '/'
        };
    }

    public getToken(formId: string): Result<string> {
        if (!formId) {
            return Result.error('FormId is required');
        }

        const cookie = this.cookieStore.get(this.COOKIE_CONFIG.name);
        if (!cookie) {
            return Result.error('No cookie found');
        }

        try {
            const tokens = JSON.parse(cookie.value);
            const token = tokens[formId];
            return token
                ? Result.success(token)
                : Result.error('No token found for the current form');
        } catch (error) {
            return Result.error(`Error parsing cookie: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    public setToken({ formId, token }: FormToken): Result<void> {
        if (!formId || !token) {
            return Result.error('FormId and token are required');
        }

        try {
            const currentValue = this.cookieStore.get(this.COOKIE_CONFIG.name)?.value;
            const tokens = currentValue ? JSON.parse(currentValue) : {};
            const updatedValue = JSON.stringify({ ...tokens, [formId]: token });

            this.cookieStore.set(
                this.COOKIE_CONFIG.name,
                updatedValue,
                this.getCookieOptions()
            );

            return Result.success(void 0);
        } catch (error) {
            return Result.error(`Failed to set token: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    public deleteToken(formId: string): Result<void> {
        if (!formId) {
            return Result.error('FormId is required');
        }

        try {
            const cookie = this.cookieStore.get(this.COOKIE_CONFIG.name);
            if (!cookie) {
                return Result.success(void 0);
            }

            const currentTokens = JSON.parse(cookie.value);
            const { [formId]: _, ...remainingTokens } = currentTokens;

            if (Object.keys(remainingTokens).length === 0) {
                this.cookieStore.delete(this.COOKIE_CONFIG.name);
            } else {
                this.cookieStore.set(
                    this.COOKIE_CONFIG.name,
                    JSON.stringify(remainingTokens),
                    this.getCookieOptions()
                );
            }

            return Result.success(void 0);
        } catch (error) {
            return Result.error(`Failed to delete token: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }
}