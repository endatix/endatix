import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useSubmissionStatus } from '../use-submission-status.hook';
import { changeStatusAction } from '../change-status.action';
import { SubmissionStatus, type SubmissionStatusType } from '@/types/subission-status';
import { toast } from 'sonner';

// Mock dependencies
vi.mock('../change-status.action', () => ({
  changeStatusAction: vi.fn(),
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('useSubmissionStatus', () => {
  const mockProps = {
    submissionId: '1',
    formId: '2',
    status: SubmissionStatus.values.new as SubmissionStatusType,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should initialize with not pending', () => {
    const { result } = renderHook(() => useSubmissionStatus(mockProps));

    expect(result.current.isPending).toBe(false);
    expect(result.current.nextStatus).toBe(SubmissionStatus.values.seen);
  });

  it('should handle successful status change', async () => {
    vi.mocked(changeStatusAction).mockResolvedValueOnce({
      success: true,
    });

    const { result } = renderHook(() => useSubmissionStatus(mockProps));

    await act(async () => {
      await result.current.handleStatusChange();
    });

    expect(changeStatusAction).toHaveBeenCalledWith({
      formId: mockProps.formId,
      submissionId: mockProps.submissionId,
      status: SubmissionStatus.values.seen,
    });
    expect(toast.success).toHaveBeenCalledWith('Status updated successfully');
  });

  it('should handle failed status change', async () => {
    const error = 'Failed to update submission status. Please try again.';
    vi.mocked(changeStatusAction).mockResolvedValueOnce({
      success: false,
      error,
    });

    const { result } = renderHook(() => useSubmissionStatus(mockProps));

    await act(async () => {
      await result.current.handleStatusChange();
    });

    expect(toast.error).toHaveBeenCalledWith(error);
  });

  it('should toggle between new and seen status', () => {
    // Test with new status
    const { result: newResult } = renderHook(() =>
      useSubmissionStatus({
        ...mockProps,
        status: SubmissionStatus.values.new,
      })
    );
    expect(newResult.current.nextStatus).toBe(SubmissionStatus.values.seen);

    // Test with seen status
    const { result: seenResult } = renderHook(() =>
      useSubmissionStatus({
        ...mockProps,
        status: SubmissionStatus.values.seen,
      })
    );
    expect(seenResult.current.nextStatus).toBe(SubmissionStatus.values.new);
  });
}); 