import { useMemo } from 'react';
import { Model } from 'survey-core';
import { Submission } from '@/types';

export function useSurveyModel(definition: string, submission?: Submission) {
    return useMemo(() => {
        const surveyModel = new Model(definition);
        if (submission) {
            try {
                surveyModel.data = JSON.parse(submission.jsonData);
                surveyModel.currentPageNo = submission.currentPage;
            } catch (error) {
                console.debug('Failed to parse submission data', error);
            }
        }
        return surveyModel;
    }, [definition, submission]);
} 