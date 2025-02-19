// @vitest-environment node

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { AuthService } from '../../shared/auth.service';
import { cookies } from 'next/headers';
import { Result } from '@/lib/result';
import { JwtService } from '../../infrastructure/jwt.service';
import type { ReadonlyRequestCookies } from 'next/dist/server/web/spec-extension/adapters/request-cookies';

// Mock dependencies
vi.mock('next/headers', () => ({
  cookies: vi.fn(),
}));

vi.mock('../../infrastructure/jwt.service', () => ({
  JwtService: vi.fn()
}));

describe('AuthService', () => {
  let authService: AuthService;
  let mockCookieStore: Partial<ReadonlyRequestCookies>;
  let mockJwtService: Partial<JwtService>;

  const testCookieOptions = {
    name: 'test-session',
    encryptionKey: 'test-secret',
    secure: false,
    httpOnly: true,
  };

  beforeEach(() => {
    mockCookieStore = {
      get: vi.fn(),
      set: vi.fn(),
      has: vi.fn(),
      getAll: vi.fn(),
      delete: vi.fn(),
    };

    // Cast to Promise to satisfy the cookies() return type
    vi.mocked(cookies).mockReturnValue(Promise.resolve(mockCookieStore as ReadonlyRequestCookies));

    mockJwtService = {
      encryptToken: vi.fn(),
      decryptToken: vi.fn(),
      decodeAccessToken: vi.fn(),
    };

    vi.mocked(JwtService).mockImplementation(() => mockJwtService as JwtService);

    authService = new AuthService(testCookieOptions);
  });

  describe('login', () => {
    const testCredentials = {
      accessToken: 'test-access-token',
      refreshToken: 'test-refresh-token', 
      username: 'test@example.com'
    };

    it('should set session cookie with encrypted token when valid access token provided', async () => {
      // Arrange
      const mockEncryptedToken = 'encrypted-jwt-token';
      const mockDecodedToken = {
        exp: Math.floor(Date.now() / 1000) + 3600 // 1 hour from now
      };
      vi.mocked(mockJwtService.decodeAccessToken!).mockReturnValue(mockDecodedToken);
      vi.mocked(mockJwtService.encryptToken!).mockResolvedValue(mockEncryptedToken);

      // Act
      await authService.login(
        testCredentials.accessToken,
        testCredentials.refreshToken,
        testCredentials.username
      );

      // Assert
      expect(mockJwtService.decodeAccessToken).toHaveBeenCalledWith(testCredentials.accessToken);
      expect(mockJwtService.encryptToken).toHaveBeenCalledWith(
        expect.objectContaining({
          sub: testCredentials.username,
          accessToken: testCredentials.accessToken,
          refreshToken: testCredentials.refreshToken
        }),
        expect.any(Date)
      );
      expect(mockCookieStore.set).toHaveBeenCalledWith(expect.objectContaining({
        name: testCookieOptions.name,
        value: mockEncryptedToken,
        httpOnly: true,
        secure: testCookieOptions.secure,
        sameSite: 'lax',
        path: '/',
        expires: expect.any(Date)
      }));
    });

    it('should not set cookie if access token is invalid', async () => {
      // Arrange
      vi.mocked(mockJwtService.decodeAccessToken!).mockReturnValue(null);

      // Act
      await authService.login(
        testCredentials.accessToken,
        testCredentials.refreshToken,
        testCredentials.username
      );

      // Assert
      expect(mockJwtService.encryptToken).not.toHaveBeenCalled();
      expect(mockCookieStore.set).not.toHaveBeenCalled();
    });

    it('should not set cookie if credentials are missing', async () => {
      // Act
      await authService.login('', '', '');

      // Assert
      expect(mockJwtService.decodeAccessToken).not.toHaveBeenCalled();
      expect(mockJwtService.encryptToken).not.toHaveBeenCalled();
      expect(mockCookieStore.set).not.toHaveBeenCalled();
    });
  });

  describe('getSession', () => {
    it('should return anonymous session when no cookie exists', async () => {
      // Arrange
      vi.mocked(mockCookieStore.get!).mockReturnValue(undefined);

      // Act
      const session = await authService.getSession();

      // Assert
      expect(session).toEqual({
        username: '',
        accessToken: '',
        refreshToken: '',
        isLoggedIn: false
      });
      expect(mockJwtService.decryptToken).not.toHaveBeenCalled();
    });

    it('should return valid session for valid JWT token', async () => {
      // Arrange
      const mockToken = 'valid-token';
      const mockPayload = {
        sub: 'test@example.com',
        accessToken: 'access-token',
        refreshToken: 'refresh-token'
      };

      vi.mocked(mockCookieStore.get!).mockReturnValue({ name: 'token', value: mockToken });
      vi.mocked(mockJwtService.decryptToken!).mockResolvedValue(Result.success(mockPayload));

      // Act
      const session = await authService.getSession();

      // Assert
      expect(mockJwtService.decryptToken).toHaveBeenCalledWith(mockToken);
      expect(session).toEqual({
        username: mockPayload.sub,
        accessToken: mockPayload.accessToken,
        refreshToken: mockPayload.refreshToken,
        isLoggedIn: true
      });
    });

    it('should return anonymous session for invalid JWT token', async () => {
      // Arrange
      const mockToken = 'invalid-token';
      vi.mocked(mockCookieStore.get!).mockReturnValue({ name: 'token', value: mockToken });
      vi.mocked(mockJwtService.decryptToken!).mockResolvedValue(Result.error('Invalid token'));

      // Act
      const session = await authService.getSession();

      // Assert
      expect(mockJwtService.decryptToken).toHaveBeenCalledWith(mockToken);
      expect(session).toEqual({
        username: '',
        accessToken: '',
        refreshToken: '',
        isLoggedIn: false
      });
    });

    it('should return anonymous session for validation error', async () => {
      // Arrange
      const mockToken = 'expired-token';
      vi.mocked(mockCookieStore.get!).mockReturnValue({ name: 'token', value: mockToken });
      vi.mocked(mockJwtService.decryptToken!).mockResolvedValue(Result.validationError('ERR_JWT_EXPIRED', 'Token expired'));

      // Act
      const session = await authService.getSession();

      // Assert
      expect(mockJwtService.decryptToken).toHaveBeenCalledWith(mockToken);
      expect(session).toEqual({
        username: '',
        accessToken: '',
        refreshToken: '',
        isLoggedIn: false
      });
    });
  });

  describe('logout', () => {
    it('should clear session cookie if it exists', async () => {
      // Arrange
      vi.mocked(mockCookieStore.has!).mockReturnValue(true);

      // Act
      await authService.logout();

      // Assert
      expect(mockCookieStore.has).toHaveBeenCalledWith(testCookieOptions.name);
      expect(mockCookieStore.set).toHaveBeenCalledWith({
        name: testCookieOptions.name,
        value: '',
        httpOnly: true,
        secure: testCookieOptions.secure,
        sameSite: 'lax',
        maxAge: 0,
        path: '/'
      });
    });

    it('should not attempt to clear cookie if it does not exist', async () => {
      // Arrange
      vi.mocked(mockCookieStore.has!).mockReturnValue(false);

      // Act
      await authService.logout();

      // Assert
      expect(mockCookieStore.has).toHaveBeenCalledWith(testCookieOptions.name);
      expect(mockCookieStore.set).not.toHaveBeenCalled();
    });
  });
});
