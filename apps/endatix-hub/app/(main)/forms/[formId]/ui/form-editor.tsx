'use client';

import { SurveyCreatorProps } from "@/components/survey-creator";
import dynamic from "next/dynamic";

const SurveyCreatorComponent = dynamic(() => import('@/components/survey-creator'), {
    ssr: false,
});

const FormEditor = (props: SurveyCreatorProps) => {
    return (
        <SurveyCreatorComponent {...props} />
    )
}

export default FormEditor;