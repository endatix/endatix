"use client"

import dynamic from 'next/dynamic';

const SurveyComponent = dynamic(() => import('@/components/survey-component'), {
    ssr: false,
});

interface SurveyJsContainerProps {
    definition: string;
    formId: string;
}

const SurveyJsContainer = ({ formId, definition }: SurveyJsContainerProps) => {
    return (
        <SurveyComponent formId = { formId }  definition = { definition } />
    )
}

export default SurveyJsContainer;