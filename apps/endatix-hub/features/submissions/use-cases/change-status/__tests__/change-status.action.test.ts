import { describe, it, expect, vi, beforeEach } from 'vitest';
import { changeStatusAction } from '../change-status.action';
import { changeStatusUseCase } from '../change-status.use-case';
import { SubmissionStatusKind } from '@/types';
import { revalidatePath } from 'next/cache';

// Mock dependencies
vi.mock('../change-status.use-case', () => ({
  changeStatusUseCase: vi.fn(),
}));

vi.mock('next/cache', () => ({
  revalidatePath: vi.fn(),
}));

describe('changeStatusAction', () => {
  const mockCommand = {
    submissionId: '1',
    formId: '2',
    status: SubmissionStatusKind.Read,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should handle successful status change', async () => {
    vi.mocked(changeStatusUseCase).mockResolvedValueOnce(true);

    const result = await changeStatusAction(mockCommand);

    expect(changeStatusUseCase).toHaveBeenCalledWith(mockCommand);
    expect(revalidatePath).toHaveBeenCalledWith(
      `/forms/${mockCommand.formId}/submissions/${mockCommand.submissionId}`
    );
    expect(result.success).toBe(true);
    expect(result.error).toBeUndefined();
  });

  it('should handle failed status change', async () => {
    vi.mocked(changeStatusUseCase).mockResolvedValueOnce(false);

    const result = await changeStatusAction(mockCommand);

    expect(changeStatusUseCase).toHaveBeenCalledWith(mockCommand);
    expect(revalidatePath).not.toHaveBeenCalled();
    expect(result.success).toBe(false);
    expect(result.error).toBe(
      'Failed to update submission status. Please try again.'
    );
  });

  it('should handle use case errors', async () => {
    vi.mocked(changeStatusUseCase).mockResolvedValueOnce(false);

    const result = await changeStatusAction(mockCommand);

    expect(changeStatusUseCase).toHaveBeenCalledWith(mockCommand);
    expect(revalidatePath).not.toHaveBeenCalled();
    expect(result.success).toBe(false);
    expect(result.error).toBe(
      'Failed to update submission status. Please try again.'
    );
  });
});
