import { JWTPayload, SignJWT, jwtVerify, decodeJwt } from "jose";
import { cookies } from "next/headers";
import { JWTInvalid } from "jose/errors";
import { cache } from "react";
import { Kind, Result } from "./result";
import { redirect, RedirectType } from "next/navigation";

export interface SessionData {
  username: string;
  accessToken: string;
  refreshToken: string;
  isLoggedIn: boolean;
}

interface CookieOptions {
  name: string;
  encryptionKey: string;
  secure: boolean;
  httpOnly: boolean;
}

interface EndatixJwtPayload extends JWTPayload {
  permission?: string[];
  role?: string[];
}

interface HubJwtPayload extends JWTPayload {
  accessToken: string;
  refreshToken: string;
  sub: string;
}

const HUB_COOKIE_OPTIONS: CookieOptions = {
  name: "session",
  encryptionKey: `${process.env.SESSION_SECRET}`,
  secure: process.env.NODE_ENV === "production",
  httpOnly: true,
};

const ANONYMOUS_SESSION: SessionData = {
  username: "",
  accessToken: "",
  refreshToken: "",
  isLoggedIn: false,
};

const SAFETY_MARGIN_IN_SECONDS = 10;

// Cached session getter for reuse
export const getSession = cache(async (): Promise<SessionData> => {
  const authService = new AuthService();
  const session = await authService.getSession();
  return session;
});

// Authentication guard for protected routes
export const ensureAuthenticated = async (): Promise<void> => {
  const currentSession = await getSession();
  if (!currentSession.isLoggedIn) {
    redirect("/login", RedirectType.push);
  }
};

export class AuthService {
  private readonly secretKey: Uint8Array;

  constructor(
    private readonly cookieOptions: CookieOptions = HUB_COOKIE_OPTIONS,
  ) {
    this.secretKey = new TextEncoder().encode(this.cookieOptions.encryptionKey);
  }

  async login(accessToken: string, refreshToken: string, username: string) {
    if (!accessToken || !refreshToken || !username) {
      return;
    }

    const jwtToken = decodeJwt<EndatixJwtPayload>(accessToken);
    if (!jwtToken?.exp) {
      return;
    }

    const expires = new Date(jwtToken.exp * 1000);
    expires.setSeconds(expires.getSeconds() - SAFETY_MARGIN_IN_SECONDS);

    const hubJwtPayload: HubJwtPayload = {
      sub: username,
      accessToken: accessToken,
      refreshToken: refreshToken,
    };
    const hubSessionToken = await this.encryptToken(hubJwtPayload, expires);

    const cookieStore = await cookies();
    cookieStore.set({
      name: this.cookieOptions.name,
      value: hubSessionToken,
      httpOnly: true,
      secure: this.cookieOptions.secure,
      sameSite: "lax",
      expires: expires,
      path: "/",
    });
  }

  async getSession(): Promise<SessionData> {
    const cookieStore = await cookies();
    const sessionCookie = cookieStore.get(this.cookieOptions.name);

    if (!sessionCookie || !sessionCookie.value) {
      return ANONYMOUS_SESSION;
    }

    const jwtTokenResult = await this.decryptToken(sessionCookie.value);
    if (jwtTokenResult.kind == Kind.Success) {
      const sessionData: SessionData = {
        isLoggedIn: true,
        accessToken: jwtTokenResult.value.accessToken,
        refreshToken: jwtTokenResult.value.refreshToken,
        username: jwtTokenResult.value.sub,
      };
      return sessionData;
    }

    if (jwtTokenResult.kind === Kind.Error) {
      console.error("Error during JWT token validaiton has occured");
    }

    return ANONYMOUS_SESSION;
  }

  async logout() {
    const cookieStore = await cookies();

    if (cookieStore.has(this.cookieOptions.name)) {
      cookieStore.set({
        name: this.cookieOptions.name,
        value: "",
        httpOnly: true,
        secure: this.cookieOptions.secure,
        sameSite: "lax",
        maxAge: 0,
        path: "/",
      });
    }
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
      const { payload } = await jwtVerify<HubJwtPayload>(
        token,
        this.secretKey,
        {
          algorithms: ["HS256"],
        },
      );

      return Result.success<HubJwtPayload>(payload);
    } catch (error: unknown) {
      if (error instanceof JWTInvalid) {
        return Result.validationError(error?.code, error.message);
      }

      return Result.error("Error during token validaiton occured");
    }
  }
}
