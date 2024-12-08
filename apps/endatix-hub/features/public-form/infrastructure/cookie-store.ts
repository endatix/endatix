import { Result } from "@/lib/result";
import { ResponseCookie } from "next/dist/compiled/@edge-runtime/cookies";
import { ReadonlyRequestCookies } from "next/dist/server/web/spec-extension/adapters/request-cookies";

type FormToken = {
    formId: string;
    token: string;
}

export class FormTokenCookieStore {
    private readonly COOKIE_CONFIG = {
        name: 'FPSK',
        expirationInDays: 7,
        secure: process.env.NODE_ENV === 'production',
        getExpires: () => new Date(Date.now() + this.COOKIE_CONFIG.expirationInDays * 24 * 60 * 60 * 1000),
        getCookieOptions: (): Partial<ResponseCookie> => ({
            httpOnly: true,
            secure: this.COOKIE_CONFIG.secure,
            sameSite: "strict",
            expires: this.COOKIE_CONFIG.getExpires()
        })
    } as const;

    constructor(private readonly cookieStore: ReadonlyRequestCookies) { }

    public getToken(formId: string): Result<string> {
        const cookie = this.cookieStore.get(this.COOKIE_CONFIG.name);
        if (!cookie || !formId) {
            return Result.error('No cookie or formId provided');
        }

        try {
            const tokens = JSON.parse(cookie.value);
            const token = tokens[formId];
            return token
                ? Result.success(token)
                : Result.error('No token found for the current form');
        } catch (error) {
            return Result.error(`Error parsing FPSK cookie. Details: ${error}`);
        }
    }

    public setToken({ formId, token }: FormToken): void {
        const currentValue = JSON.parse(
            this.cookieStore.get(this.COOKIE_CONFIG.name)?.value || '{}'
        );
        const updatedValue = JSON.stringify({ ...currentValue, [formId]: token });
        this.cookieStore.set(
            this.COOKIE_CONFIG.name,
            updatedValue,
            this.COOKIE_CONFIG.getCookieOptions()
        );
    }

    private setTokens(tokens: Record<string, string>): void {
        this.cookieStore.set(
            this.COOKIE_CONFIG.name,
            JSON.stringify(tokens),
            this.COOKIE_CONFIG.getCookieOptions()
        );
    }

    public deleteToken(formId: string): void {
        const currentTokens = JSON.parse(
            this.cookieStore.get(this.COOKIE_CONFIG.name)?.value || '{}'
        );
        const { [formId]: _, ...remainingTokens } = currentTokens;

        if (Object.keys(remainingTokens).length === 0) {
            this.cookieStore.delete(this.COOKIE_CONFIG.name);
        } else {
            this.setTokens(remainingTokens);
        }
    }
}