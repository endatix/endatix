import { describe, it, expect, vi, beforeEach, Mock } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { submissionQueue } from '../submission-queue/submission-queue';
import { useSubmissionQueue } from '../submission-queue/use-submission-queue';

// The mock path should match the import path exactly
vi.mock('../submission-queue/submission-queue', () => ({
    submissionQueue: {
        enqueue: vi.fn(),
        clear: vi.fn()
    }
}));

describe('useSubmissionQueue', () => {
    const mockFormId = 'test-form-id';

    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('should provide queue management functions', () => {
        // Arrange & Act
        const { result } = renderHook(() => useSubmissionQueue(mockFormId));

        // Assert
        expect(typeof result.current.enqueueSubmission).toBe('function');
        expect(typeof result.current.clearQueue).toBe('function');
    });

    it('should enqueue submission with correct formId', () => {
        // Arrange
        const { result } = renderHook(() => useSubmissionQueue(mockFormId));
        const submissionData = {
            jsonData: '{"test": true}',
            isComplete: false,
            currentPage: 1
        };

        // Act
        act(() => {
            result.current.enqueueSubmission(submissionData);
        });

        // Assert
        expect(submissionQueue.enqueue).toHaveBeenCalledWith({
            formId: mockFormId,
            data: submissionData
        });
    });

    it('should clear the queue when clearQueue is called', () => {
        // Arrange
        const { result } = renderHook(() => useSubmissionQueue(mockFormId));

        // Act
        act(() => {
            result.current.clearQueue();
        });

        // Assert
        expect(submissionQueue.clear).toHaveBeenCalled();
    });

    it('should maintain formId in closure for multiple submissions', () => {
        // Arrange
        const { result } = renderHook(() => useSubmissionQueue(mockFormId));
        const submissions = [
            {
                jsonData: '{"page": 1}',
                isComplete: false,
                currentPage: 1
            },
            {
                jsonData: '{"page": 2}',
                isComplete: false,
                currentPage: 2
            }
        ];

        // Act
        act(() => {
            submissions.forEach(data => {
                result.current.enqueueSubmission(data);
            });
        });

        // Assert
        submissions.forEach(data => {
            expect(submissionQueue.enqueue).toHaveBeenCalledWith({
                formId: mockFormId,
                data
            });
        });
    });

    it('should update formId when hook is rerendered with new formId', () => {
        // Arrange
        const { result, rerender } = renderHook(
            (formId: string) => useSubmissionQueue(formId),
            { initialProps: mockFormId }
        );
        const newFormId = 'new-form-id';
        const submissionData = {
            jsonData: '{"test": true}',
            isComplete: false,
            currentPage: 1
        };

        // Act - First submission with original formId
        act(() => {
            result.current.enqueueSubmission(submissionData);
        });

        // Rerender with new formId
        rerender(newFormId);

        // Act - Second submission with new formId
        act(() => {
            result.current.enqueueSubmission(submissionData);
        });

        // Assert
        expect(submissionQueue.enqueue).toHaveBeenCalledTimes(2);
        expect(submissionQueue.enqueue).toHaveBeenNthCalledWith(1, {
            formId: mockFormId,
            data: submissionData
        });
        expect(submissionQueue.enqueue).toHaveBeenNthCalledWith(2, {
            formId: newFormId,
            data: submissionData
        });
    });

    it('should call enqueue even with empty formId', () => {
        // Arrange
        const { result } = renderHook(() => useSubmissionQueue(''));
        const submissionData = {
            jsonData: '{"test": true}',
            isComplete: false,
            currentPage: 1
        };

        // Act
        act(() => {
            result.current.enqueueSubmission(submissionData);
        });

        // Assert
        expect(submissionQueue.enqueue).toHaveBeenCalledWith({
            formId: '',
            data: submissionData
        });
    });
}); 