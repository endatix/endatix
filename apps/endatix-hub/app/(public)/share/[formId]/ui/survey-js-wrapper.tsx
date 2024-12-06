"use client"

import dynamic from 'next/dynamic'
import 'survey-core/defaultV2.css'

const SurveyComponent = dynamic(() => import('./survey-component'), {
    ssr: false,
});

interface SurveyJsWrapperProps {
    definition: string;
    formId: string;
}

const SurveyJsWrapper = ({ formId, definition }: SurveyJsWrapperProps) => {
    return (
        <SurveyComponent formId = { formId }  definition = { definition } />
    )
}

export default SurveyJsWrapper;