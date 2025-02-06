import { describe, it, expect, vi, beforeEach } from 'vitest';
import { changeStatusUseCase } from '../change-status.use-case';
import { updateSubmissionStatus } from '@/services/api';
import { SubmissionStatusKind } from '@/types';

// Mock the API service
vi.mock('@/services/api', () => ({
  updateSubmissionStatus: vi.fn(),
}));

describe('changeStatusUseCase', () => {
  const mockParams = {
    formId: '1',
    submissionId: '2',
    status: SubmissionStatusKind.Approved
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should successfully change status', async () => {
    vi.mocked(updateSubmissionStatus).mockResolvedValueOnce({
      formId: mockParams.submissionId,
      status: mockParams.status,
      dateUpdated: new Date(),
    });

    const result = await changeStatusUseCase(mockParams);

    expect(updateSubmissionStatus).toHaveBeenCalledWith(
      mockParams.formId,
      mockParams.submissionId,
      mockParams.status
    );
    expect(result).toBe(true);
  });

  it('should handle API errors', async () => {
    const error = new Error('API Error');
    vi.mocked(updateSubmissionStatus).mockRejectedValueOnce(error);

    const result = await changeStatusUseCase(mockParams);

    expect(result).toBe(false);
  });

  it('should handle network errors', async () => {
    vi.mocked(updateSubmissionStatus).mockRejectedValueOnce(
      new Error('Network error')
    );

    const result = await changeStatusUseCase(mockParams);

    expect(result).toBe(false);
  });
}); 