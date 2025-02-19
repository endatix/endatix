// @vitest-environment node

import { describe, it, expect, beforeEach, vi } from "vitest";
import { JwtService } from "../../infrastructure/jwt.service";
import { HubJwtPayload } from "../../infrastructure/jwt.types";
import { ErrorType, Result } from "@/lib/result";

describe("JwtService", () => {
  let jwtService: JwtService;
  const secretKey = new TextEncoder().encode("test-secret-key");
  const testPayload: HubJwtPayload = {
    sub: "test@example.com",
    accessToken: "access-token",
    refreshToken: "refresh-token",
  };

  beforeEach(() => {
    vi.clearAllMocks();
    jwtService = new JwtService(secretKey);
  });

  describe("encryptToken", () => {
    it("should successfully encrypt a token with payload and expiration", async () => {
      // Arrange
      const expiration = new Date();
      expiration.setHours(expiration.getHours() + 1);

      // Act
      const token = await jwtService.encryptToken(testPayload, expiration);

      // Assert
      expect(token).toBeDefined();
      expect(typeof token).toBe("string");
      expect(token.split(".")).toHaveLength(3); // JWT format: header.payload.signature
    });
  });

  describe("decryptToken", () => {
    it("should successfully decrypt a valid token", async () => {
      // Arrange
      const expiration = new Date();
      expiration.setHours(expiration.getHours() + 1);
      const token = await jwtService.encryptToken(testPayload, expiration);

      // Act
      const result = await jwtService.decryptToken(token);

      // Assert
      expect(Result.isSuccess(result)).toBe(true);
      if (Result.isSuccess(result)) {
        expect(result.value.sub).toBe(testPayload.sub);
        expect(result.value.accessToken).toBe(testPayload.accessToken);
        expect(result.value.refreshToken).toBe(testPayload.refreshToken);
      }
    });

    it("should return an error when the token is in invalid format", async () => {
      // Arrange
      const invalidToken = "invalid.token.format";

      // Act
      const result = await jwtService.decryptToken(invalidToken);

      // Assert
      expect(Result.isError(result)).toBe(true);
      if (Result.isError(result)) {
        expect(result.errorType).toBe(ErrorType.ValidationError);
        expect(result.details).toContain("JWS Protected Header is invalid");
      }
    });

    it("should return validation error for expired token", async () => {
      // Arrange
      const expiration = new Date();
      expiration.setSeconds(expiration.getSeconds() - 1); // Set expiration to the past
      const token = await jwtService.encryptToken(testPayload, expiration);

      // Act
      const result = await jwtService.decryptToken(token);

      // Assert
      expect(Result.isError(result)).toBe(true);
      if (Result.isError(result)) {
        expect(result.errorType).toBe(ErrorType.ValidationError);
        expect(result.details).toContain("timestamp check failed");
      }
    });

    it("should return error for token signed with different key", async () => {
      // Arrange
      const differentKey = new TextEncoder().encode("different-secret-key");
      const differentService = new JwtService(differentKey);
      const expiration = new Date();
      expiration.setHours(expiration.getHours() + 1);
      const token = await differentService.encryptToken(
        testPayload,
        expiration,
      );

      // Act
      const result = await jwtService.decryptToken(token);

      // Assert
      expect(Result.isError(result)).toBe(true);
      if (Result.isError(result)) {
        expect(result.errorType).toBe(ErrorType.ValidationError);
        expect(result.details).toBe("signature verification failed");
      }
    });
  });
});
