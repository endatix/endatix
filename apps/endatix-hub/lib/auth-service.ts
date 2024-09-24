import { JWTPayload, SignJWT, jwtVerify, decodeJwt } from "jose";
import { cookies } from "next/headers";
import { revalidatePath } from "next/cache";
import { JWTInvalid } from "jose/errors";
import { cache } from "react";

enum Kind {
    Success,
    Error
}

type Success<T> = {
    kind: Kind.Success,
    value: T

};

enum ErrorType {
    ValidationError,
    Error
}

type Error = {
    kind: Kind.Error,
    errorType: ErrorType,
    message: string,
    details?: string
};

type Result<T> = Success<T> | Error;

export function Success<T>(value: T): Success<T> {
    return { kind: Kind.Success, value };
}

export function Error<T>(error: Error): Result<T> {
    return {
        kind: Kind.Error,
        errorType: error.errorType,
        message: error.message,
        details: error.details
    };
}

export function isSuccess<T>(result: Result<T>): boolean {
    return result.kind === Kind.Success;
}

export function isError<T>(result: Result<T>): boolean {
    return result.kind === Kind.Error;
}

export interface SessionData {
    username: string;
    token: string;
    isLoggedIn: boolean;
}

interface CookieOptions {
    name: string,
    encryptionKey: string
    secure: boolean
    httpOnly: boolean

}

interface EndatixJwtPayload extends JWTPayload {
    permission?: string[],
    role?: string[]
}

interface HubJwtPayload extends JWTPayload {
    apiToken: string,
    sub: string
}


const HUB_COOKIE_OPTIONS: CookieOptions = {
    name: "endatix-hub.session",
    encryptionKey: `${process.env.SESSION_SECRET}`,
    secure: process.env.NODE_ENV === "production",
    httpOnly: true
};

const ANONYMOUS_SESSION: SessionData = {
    username: "",
    token: "",
    isLoggedIn: false,
};

export class AuthService {
    private readonly secretKey: Uint8Array;

    constructor(private readonly cookieOptions: CookieOptions = HUB_COOKIE_OPTIONS) {
        this.secretKey = new TextEncoder().encode(this.cookieOptions.encryptionKey);
    }

    async login(authToken: string, username: string) {
        if (!authToken || !username) {
            return;
        }

        const jwtToken = decodeJwt<EndatixJwtPayload>(authToken);
        if (!jwtToken?.exp) {
            return;
        }

        const expires = new Date(jwtToken.exp * 1000);
        expires.setSeconds(expires.getSeconds() - 10);

        const hubJwtPayload: HubJwtPayload = {
            sub: username,
            apiToken: authToken
        };
        const hubSessionToken = await this.encryptToken(hubJwtPayload, expires);

        cookies().set({
            name: this.cookieOptions.name,
            value: hubSessionToken,
            httpOnly: true,
            secure: this.cookieOptions.secure,
            expires: expires,
            path: '/',
        });

        revalidatePath("/login");
    }

    async getSession(): Promise<SessionData> {
        const sessionCookie = cookies().get(this.cookieOptions.name);

        if (!sessionCookie || !sessionCookie.value) {
            return ANONYMOUS_SESSION;
        }

        const jwtTokenResult = await this.decryptToken(sessionCookie.value);
        if (jwtTokenResult.kind == Kind.Success) {
            const sessionData: SessionData = {
                isLoggedIn: true,
                token: jwtTokenResult.value.apiToken,
                username: jwtTokenResult.value.sub
            };
            return sessionData;
        }

        if (jwtTokenResult.kind === Kind.Error) {
            console.error("Error during JWT token validaiton has occured");
        }

        return ANONYMOUS_SESSION;
    }


    async logout() {
        if (cookies().has(this.cookieOptions.name)) {
            cookies().delete(this.cookieOptions.name);
        }

        revalidatePath("/login");
    }

    private async encryptToken(payload: JWTPayload, expiration: Date) {
        return await new SignJWT(payload)
            .setProtectedHeader({ alg: "HS256" })
            .setIssuedAt()
            .setExpirationTime(expiration)
            .sign(this.secretKey);
    }

    private async decryptToken(token: string): Promise<Result<HubJwtPayload>> {
        try {
            const { payload } = await jwtVerify<HubJwtPayload>(token, this.secretKey, {
                algorithms: ["HS256"]
            });

            return Success<HubJwtPayload>(payload);

        } catch (error: unknown) {
            if (error instanceof JWTInvalid) {
                return Error({
                    kind: Kind.Error,
                    errorType: ErrorType.ValidationError,
                    message: error?.code,
                    details: error.message
                });
            }

            return Error({
                kind: Kind.Error,
                errorType: ErrorType.Error,
                message: "Error during token validaiton occured"
            });
        }
    }
}

export const getSession = cache(async () : Promise<SessionData> => {
    const authService = new AuthService();
    const session = await authService.getSession();
    return session
})