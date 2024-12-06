import { describe, it, expect, vi, Mock, beforeEach } from 'vitest'
import { submitFormAction } from '@/app/(public)/share/[formId]/submit-form.action'
import { createSubmission, updateExistingSubmission } from '@/services/api'
import { cookies } from 'next/headers'
import { ErrorType, Result } from '@/lib/result'

vi.mock('next/headers', () => ({
  cookies: vi.fn(() => ({
    get: vi.fn(),
    set: vi.fn(),
    delete: vi.fn()
  }))
}))

vi.mock('@/services/api', () => ({
  createSubmission: vi.fn(),
  updateExistingSubmission: vi.fn()
}))

describe('submitFormAction', () => {
  const mockCookieStore = {
    get: vi.fn(),
    set: vi.fn(),
    delete: vi.fn()
  }

  beforeEach(() => {
    vi.clearAllMocks();
    (cookies as Mock).mockReturnValue(mockCookieStore);
  })

  it('should create new submission and update cookie when no token exists', async () => {
    // Arrange
    mockCookieStore.get.mockReturnValue(undefined)
    const mockSubmissionData = {
      jsonData: '{"test": true}',
      isComplete: false,
      currentPage: 1
    }

    const mockCreateResponse = {
      token: 'new-token',
      isComplete: false
    };
    (createSubmission as Mock).mockResolvedValue(mockCreateResponse)

    // Act
    const result = await submitFormAction('form-1', mockSubmissionData)

    // Assert
    expect(createSubmission).toHaveBeenCalledWith('form-1', mockSubmissionData)
    expect(mockCookieStore.set).toHaveBeenCalledWith(
      'FPSK',
      expect.stringContaining('new-token'),
      expect.any(Object)
    )
    expect(Result.isSuccess(result)).toBe(true)
  })

  it('should update existing submission and delete cookie value when token exists', async () => {
    // Arrange
    mockCookieStore.get.mockReturnValue({
      value: JSON.stringify({ 'form-1': 'existing-token' })
    })

    const mockSubmissionData = {
      jsonData: '{"test": true}',
      isComplete: true,
      currentPage: 2
    }

    const mockUpdateResponse = {
      isComplete: true
    };
    (updateExistingSubmission as Mock).mockResolvedValue(mockUpdateResponse)

    // Act
    const result = await submitFormAction('form-1', mockSubmissionData)

    // Assert
    expect(updateExistingSubmission).toHaveBeenCalledWith(
      'form-1',
      'existing-token',
      mockSubmissionData
    )
    expect(mockCookieStore.delete).toHaveBeenCalledWith('FPSK')
    expect(Result.isSuccess(result)).toBe(true)
  })

  it('should return a Result.error when the submission API call fails', async () => {
    // Arrange
    mockCookieStore.get.mockReturnValue(undefined);
    (createSubmission as Mock).mockRejectedValue(new Error('API Error'));

    // Act
    const result = await submitFormAction('form-1', {
      jsonData: '{"test": true}',
      isComplete: false
    })

    // Assert
    expect(Result.isError(result)).toBe(true)
    expect(result.kind).toBe(ErrorType.Error);
  })
}) 