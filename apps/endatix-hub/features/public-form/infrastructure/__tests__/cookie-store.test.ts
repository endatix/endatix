import { describe, it, expect, vi, beforeEach } from 'vitest';
import { FormTokenCookieStore } from '../cookie-store';
import { Result, Success } from '@/lib/result';

describe('FormTokenCookieStore', () => {
    const mockCookieStore = {
        get: vi.fn(),
        set: vi.fn(),
        delete: vi.fn(),
    };

    beforeEach(() => {
        vi.clearAllMocks();
        process.env.NEXT_FORMS_COOKIE_NAME = 'TEST_COOKIE';
        process.env.NEXT_FORMS_COOKIE_DURATION_DAYS = '7';
    });

    it('should initialize with environment variables', () => {
        const store = new FormTokenCookieStore(mockCookieStore as any);
        expect(store).toBeDefined();
    });

    it('should throw error if environment variables are not set', () => {
        // Arrange
        process.env.NEXT_FORMS_COOKIE_NAME = '';

        // Act & Assert
        expect(() => {
            new FormTokenCookieStore(mockCookieStore as any);
        }).toThrow('NEXT_FORMS_COOKIE_NAME environment variable is not set');
    });

    it('should get token successfully', () => {
        // Arrange
        const store = new FormTokenCookieStore(mockCookieStore as any);
        mockCookieStore.get.mockReturnValue({
            value: JSON.stringify({ 'form-1': 'token-1' })
        });

        // Act
        const result = store.getToken('form-1');

        // Assert
        expect(Result.isSuccess(result)).toBe(true);
        if (Result.isSuccess(result)) {
            expect(result.value).toBe('token-1');
        }
    });

    it('should handle missing token', () => {
        const store = new FormTokenCookieStore(mockCookieStore as any);
        mockCookieStore.get.mockReturnValue({
            value: JSON.stringify({})
        });

        const result = store.getToken('non-existent');
        expect(Result.isError(result)).toBe(true);
        if (Result.isError(result)) {
            expect(result.message).toBe('No token found for the current form');
        }
    });

    it('should set token successfully', () => {
        const store = new FormTokenCookieStore(mockCookieStore as any);
        mockCookieStore.get.mockReturnValue({
            value: JSON.stringify({})
        });

        const result = store.setToken({ formId: 'form-1', token: 'new-token' });
        expect(Result.isSuccess(result)).toBe(true);
        expect(mockCookieStore.set).toHaveBeenCalledWith(
            'TEST_COOKIE',
            JSON.stringify({ 'form-1': 'new-token' }),
            expect.objectContaining({
                httpOnly: true,
                sameSite: 'strict'
            })
        );
    });

    it('should delete token successfully', () => {
        const store = new FormTokenCookieStore(mockCookieStore as any);
        mockCookieStore.get.mockReturnValue({
            value: JSON.stringify({ 'form-1': 'token-1', 'form-2': 'token-2' })
        });

        const result = store.deleteToken('form-1');
        expect(Result.isSuccess(result)).toBe(true);
        expect(mockCookieStore.set).toHaveBeenCalledWith(
            'TEST_COOKIE',
            JSON.stringify({ 'form-2': 'token-2' }),
            expect.any(Object)
        );
    });

    it('should delete cookie when last token is removed', () => {
        const store = new FormTokenCookieStore(mockCookieStore as any);
        mockCookieStore.get.mockReturnValue({
            value: JSON.stringify({ 'form-1': 'token-1' })
        });

        const result = store.deleteToken('form-1');
        expect(Result.isSuccess(result)).toBe(true);
        expect(mockCookieStore.delete).toHaveBeenCalledWith('TEST_COOKIE');
    });

    it('should handle invalid JSON in cookie', () => {
        const store = new FormTokenCookieStore(mockCookieStore as any);
        mockCookieStore.get.mockReturnValue({
            value: 'invalid-json'
        });

        const result = store.getToken('form-1');
        expect(Result.isError(result)).toBe(true);
        if (Result.isError(result)) {
            expect(result.message).toContain('Error parsing cookie');
        }
    });
}); 