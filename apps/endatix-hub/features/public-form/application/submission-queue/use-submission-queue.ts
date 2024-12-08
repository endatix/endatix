import { useCallback } from 'react';
import { submissionQueue } from './submission-queue';
import { SubmissionData } from '../actions/submit-form.action';

export function useSubmissionQueue(formId: string) {
    const enqueueSubmission = useCallback((data: SubmissionData) => {
            submissionQueue.enqueue({ formId, data });
        }, [formId]);

    const clearQueue = useCallback(() => {
        submissionQueue.clear();
    }, []);

    return {
        enqueueSubmission,
        clearQueue
    };
}