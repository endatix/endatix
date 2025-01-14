'use client'

import { Submission } from '@/types';
import dynamic from 'next/dynamic'
import { useEffect, useState } from 'react';

const SurveyComponent = dynamic(() => import('./survey-component'), {
    ssr: false,
});

interface SurveyJsWrapperProps {
    definition: string;
    formId: string;
    submission?: Submission | undefined
}

const SurveyJsWrapper = ({ formId, definition, submission }: SurveyJsWrapperProps) => {
    const [cookiesEnabled, setCookiesEnabled] = useState(true);

    const areCookiesEnabled = (): boolean => {
        if (!navigator.cookieEnabled) {
            return false;
        }

        if (!document.cookie) {
            document.cookie = "fkst";
            if (document.cookie.length === 0) {
                return false;
            }

            document.cookie = "";
        }

        return true;
    }

    useEffect(() => {
        const cookiesEnabled = areCookiesEnabled();
        if (!cookiesEnabled) {
            setCookiesEnabled(false);
        }
    }, [cookiesEnabled]);

    return (
        <>
            {cookiesEnabled ? (
                <SurveyComponent
                    formId={formId}
                    definition={definition}
                    submission={submission}
                />
            ) : (
                <div>Cookies are not enabled. You must enable cookies to continue.</div>
            )}
        </>
    );
}

export default SurveyJsWrapper;