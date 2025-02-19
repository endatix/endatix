import { JWTPayload, SignJWT, jwtVerify } from "jose";
import {
  JWSInvalid,
  JWSSignatureVerificationFailed,
  JWTExpired,
  JWTInvalid,
} from "jose/errors";
import { Result } from "@/lib/result";
import { HubJwtPayload } from "./jwt.types";
import { decodeJwt } from "jose";
import { EndatixJwtPayload } from "./jwt.types";

export class JwtService {
  constructor(private readonly secretKey: Uint8Array) {}

  async encryptToken(payload: JWTPayload, expiration: Date): Promise<string> {
    return await new SignJWT(payload)
      .setProtectedHeader({ alg: "HS256" })
      .setIssuedAt()
      .setExpirationTime(expiration)
      .sign(this.secretKey);
  }

  async decryptToken(token: string): Promise<Result<HubJwtPayload>> {
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
      console.log("error", error);
      if (
        error instanceof JWTInvalid ||
        error instanceof JWTExpired ||
        error instanceof JWSInvalid ||
        error instanceof JWSSignatureVerificationFailed
      ) {
        return Result.validationError(error.code, error.message);
      }

      return Result.error("Error during token validation occurred");
    }
  }

  decodeAccessToken(accessToken: string): EndatixJwtPayload | null {
    try {
      return decodeJwt<EndatixJwtPayload>(accessToken);
    } catch (error) {
      console.error('Failed to decode access token:', error);
      return null;
    }
  }
}
