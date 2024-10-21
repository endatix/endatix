'use client';

import dynamic from 'next/dynamic';
import { Model } from 'survey-react-ui';
import { DefineFormContext } from '@/services/api';
import { useEffect, useState } from 'react';

const PreviewForm = dynamic(() => import('./preview-form'), {
    ssr: false,
});

const PreviewFormContainer = () => {
    const [model, setModel] = useState<Model | null>(null);

    useEffect(() => {
        const getSurveyJsonFromLocalStorage = (): DefineFormContext => {
            const surveyJson = localStorage.getItem('theForm');
            return surveyJson ? JSON.parse(surveyJson) : {} as DefineFormContext;
        };

        const surveyModel = new Model(getSurveyJsonFromLocalStorage().definition);
        setModel(surveyModel);
    }, []);

    return (
        <PreviewForm model={model} />
    )
}

export default PreviewFormContainer;