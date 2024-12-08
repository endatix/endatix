import { describe, it, expect, vi, beforeEach, afterEach, Mock } from 'vitest';
import { SubmissionQueue } from '../submission-queue/submission-queue';
import { submitFormAction } from '../actions/submit-form.action';
import { Result } from '@/lib/result';

// Mock the submitFormAction
vi.mock('../actions/submit-form.action', () => ({
    submitFormAction: vi.fn()
}));

describe('SubmissionQueue', () => {
    let queue: SubmissionQueue;
    const mockSubmitForm = submitFormAction as Mock;

    beforeEach(() => {
        queue = new SubmissionQueue();
        vi.clearAllMocks();
        vi.useFakeTimers();
    });

    afterEach(() => {
        vi.runOnlyPendingTimers();
        vi.useRealTimers();
    });

    it('should process items in queue sequentially', async () => {
        // Arrange
        mockSubmitForm.mockResolvedValueOnce(Result.success({ isSuccess: true }));
        mockSubmitForm.mockResolvedValueOnce(Result.success({ isSuccess: true }));

        const items = [
            {
                formId: '1',
                data: {
                    jsonData: '{"test": 1}',
                    isComplete: false,
                    currentPage: 0
                }
            },
            {
                formId: '2',
                data: {
                    jsonData: '{"test": 2}',
                    isComplete: false,
                    currentPage: 1
                }
            }
        ];

        // Act
        items.forEach(item => queue.enqueue(item));
        await vi.runAllTimersAsync();

        // Assert
        expect(mockSubmitForm).toHaveBeenCalledTimes(2);
        expect(mockSubmitForm).toHaveBeenNthCalledWith(1, items[0].formId, items[0].data);
        expect(mockSubmitForm).toHaveBeenNthCalledWith(2, items[1].formId, items[1].data);
    });

    it('should not process new items while processing current item', async () => {
        // Arrange
        let resolveFirst: (value: unknown) => void;
        const firstSubmission = new Promise(resolve => {
            resolveFirst = resolve;
        });
        
        mockSubmitForm.mockImplementationOnce(() => firstSubmission);
        mockSubmitForm.mockResolvedValueOnce(Result.success({ isSuccess: true }));

        // Act
        queue.enqueue({
            formId: '1',
            data: {
                jsonData: '{"test": 1}',
                isComplete: false,
                currentPage: 0
            }
        });

        // Start processing first item
        vi.runAllTimers();

        queue.enqueue({
            formId: '2',
            data: {
                jsonData: '{"test": 2}',
                isComplete: false,
                currentPage: 1
            }
        });

        // Assert
        expect(mockSubmitForm).toHaveBeenCalledTimes(1);
        
        // Complete first submission
        resolveFirst!(Result.success({ isSuccess: true }));
        await vi.runAllTimersAsync();

        expect(mockSubmitForm).toHaveBeenCalledTimes(2);
    });

    it('should handle submission errors and continue processing', async () => {
        // Arrange
        const consoleSpy = vi.spyOn(console, 'debug').mockImplementation(() => {});
        mockSubmitForm.mockRejectedValueOnce(new Error('Network error'));
        mockSubmitForm.mockResolvedValueOnce(Result.success({ isSuccess: true }));

        // Act
        queue.enqueue({
            formId: '1',
            data: {
                jsonData: '{"test": 1}',
                isComplete: false,
                currentPage: 0
            }
        });

        queue.enqueue({
            formId: '2',
            data: {
                jsonData: '{"test": 2}',
                isComplete: false,
                currentPage: 1
            }
        });

        await vi.runAllTimersAsync();

        // Assert
        expect(consoleSpy).toHaveBeenCalledWith(
            'Error processing partial submission:',
            expect.any(Error)
        );
        expect(mockSubmitForm).toHaveBeenCalledTimes(2);
    });

    it('should clear queue and stop processing', async () => {
        // Arrange
        mockSubmitForm.mockResolvedValue(Result.success({ isSuccess: true }));

        // Act
        queue.enqueue({
            formId: '1',
            data: {
                jsonData: '{"test": 1}',
                isComplete: false,
                currentPage: 0
            }
        });

        queue.enqueue({
            formId: '2',
            data: {
                jsonData: '{"test": 2}',
                isComplete: false,
                currentPage: 1
            }
        });

        queue.clear();
        await vi.runAllTimersAsync();

        // Assert
        expect(mockSubmitForm).toHaveBeenCalledTimes(1); // Only one item was submitted before clearing the queue
        expect(queue.queueLength).toBe(0);       
    });

    it('should handle failed submissions with Result.error', async () => {
        // Arrange
        const consoleSpy = vi.spyOn(console, 'debug').mockImplementation(() => {});
        mockSubmitForm.mockResolvedValue(Result.error('Submission failed'));

        // Act
        queue.enqueue({
            formId: '1',
            data: {
                jsonData: '{"test": 1}',
                isComplete: false,
                currentPage: 0
            }
        });

        await vi.runAllTimersAsync();

        // Assert
        expect(consoleSpy).toHaveBeenCalledWith(
            'Failed to submit form',
            'Submission failed'
        );
    });

    it('should process items added while processing previous items', async () => {
        // Arrange
        mockSubmitForm.mockImplementation(() => 
            Promise.resolve(Result.success({ isSuccess: true }))
        );

        // Act
        queue.enqueue({
            formId: '1',
            data: {
                jsonData: '{"test": 1}',
                isComplete: false,
                currentPage: 0
            }
        });

        await vi.runAllTimersAsync();

        queue.enqueue({
            formId: '2',
            data: {
                jsonData: '{"test": 2}',
                isComplete: false,
                currentPage: 1
            }
        });

        await vi.runAllTimersAsync();

        // Assert
        expect(mockSubmitForm).toHaveBeenCalledTimes(2);
    });
}); 