import { Result } from "@/lib/result";
import { RequestCookie, ResponseCookie } from "next/dist/compiled/@edge-runtime/cookies";
import { ReadonlyRequestCookies } from "next/dist/server/web/spec-extension/adapters/request-cookies";

export const TOKENS_COOKIE_OPTIONS = {
    name: 'FPSK',
    secure: process.env.NODE_ENV === 'production',
    expirationInDays: 7,
    getExpires: () => new Date(Date.now() + TOKENS_COOKIE_OPTIONS.expirationInDays * 24 * 60 * 60 * 1000),
    getCookieOptions: (): Partial<ResponseCookie> => ({
        httpOnly: true,
        secure: TOKENS_COOKIE_OPTIONS.secure,
        sameSite: "strict",
        expires: TOKENS_COOKIE_OPTIONS.getExpires()
    })
};


export function getTokenFromCookie(tokensCookie: RequestCookie | undefined, formId: string): Result<string> {
    if (!tokensCookie || !formId) {
        return Result.error('No cookie or formId provided');
    }

    try {
        const partialTokens = JSON.parse(tokensCookie.value);
        const tokenForCurrentForm = partialTokens[formId];

        return tokenForCurrentForm ?
            Result.success(tokenForCurrentForm) :
            Result.error('No token found for the current form');
    } catch (error) {
        return Result.error('Error parsing FPSK cookie. Details: ' + error);
    }
}

export function deleteTokenFromCookie(cookieStore: ReadonlyRequestCookies, formId: string) {
    const partialTokens = JSON.parse(cookieStore.get(TOKENS_COOKIE_OPTIONS.name)?.value || '{}');
    const { [formId]: _, ...remainingTokens } = partialTokens;

    if (Object.keys(remainingTokens).length === 0) {
        cookieStore.delete(TOKENS_COOKIE_OPTIONS.name);
    } else {
        const cookieValue = JSON.stringify(remainingTokens);
        const cookieOptions = TOKENS_COOKIE_OPTIONS.getCookieOptions();
        cookieStore.set(TOKENS_COOKIE_OPTIONS.name, cookieValue, cookieOptions);
    }
}

export function setTokenInCookie(cookieStore: ReadonlyRequestCookies, formId: string, token: string) {
    const cookieValue = JSON.stringify({ [formId]: token });
    const cookieOptions = TOKENS_COOKIE_OPTIONS.getCookieOptions();
    cookieStore.set(TOKENS_COOKIE_OPTIONS.name, cookieValue, cookieOptions);
}   