import { describe, it, expect, vi, beforeEach } from 'vitest';
import { changePasswordAction } from '../change-password.action';
import { changePassword } from '@/services/api';

// Mock the API service
vi.mock('@/services/api', () => ({
  changePassword: vi.fn(),
}));

describe('changePasswordAction', () => {
  const mockFormData = new FormData();
  
  beforeEach(() => {
    vi.clearAllMocks();
    mockFormData.delete('currentPassword');
    mockFormData.delete('newPassword');
    mockFormData.delete('confirmPassword');
  });

  it('should validate required fields', async () => {
    const result = await changePasswordAction({ success: false }, mockFormData);

    expect(result.success).toBe(false);
    expect(result.errors).toBeDefined();
    expect(result.errorMessage).toContain('Could not change password');
  });

  it('should validate password length', async () => {
    mockFormData.set('currentPassword', '123');
    mockFormData.set('newPassword', '123');
    mockFormData.set('confirmPassword', '123');

    const result = await changePasswordAction({ success: false }, mockFormData);

    expect(result.success).toBe(false);
    expect(result.errors?.currentPassword).toBeDefined();
    expect(result.errors?.newPassword).toBeDefined();
  });

  it('should validate password match', async () => {
    mockFormData.set('currentPassword', 'password123');
    mockFormData.set('newPassword', 'newpassword123');
    mockFormData.set('confirmPassword', 'different123');

    const result = await changePasswordAction({ success: false }, mockFormData);

    expect(result.success).toBe(false);
    expect(result.errors?.confirmPassword).toBeDefined();
  });

  it('should call API and return success when validation passes', async () => {
    const validPassword = 'validPassword123';
    mockFormData.set('currentPassword', validPassword);
    mockFormData.set('newPassword', validPassword);
    mockFormData.set('confirmPassword', validPassword);

    vi.mocked(changePassword).mockResolvedValueOnce(undefined);

    const result = await changePasswordAction({ success: false }, mockFormData);

    expect(changePassword).toHaveBeenCalledWith(
      validPassword,
      validPassword,
      validPassword
    );
    expect(result.success).toBe(true);
    expect(result.errors).toEqual({});
    expect(result.errorMessage).toBe('');
  });

  it('should handle API errors', async () => {
    const validPassword = 'validPassword123';
    mockFormData.set('currentPassword', validPassword);
    mockFormData.set('newPassword', validPassword);
    mockFormData.set('confirmPassword', validPassword);

    const error = new Error('API Error');
    vi.mocked(changePassword).mockRejectedValueOnce(error);

    const result = await changePasswordAction({ success: false }, mockFormData);

    expect(result.success).toBe(false);
    expect(result.errorMessage).toContain('Failed to change password');
  });
}); 