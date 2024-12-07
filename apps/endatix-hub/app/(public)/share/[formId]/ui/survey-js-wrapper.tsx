"use client"

import dynamic from 'next/dynamic'
import { useEffect, useState } from 'react';
import 'survey-core/defaultV2.css'

const SurveyComponent = dynamic(() => import('./survey-component'), {
    ssr: false,
});

interface SurveyJsWrapperProps {
    definition: string;
    formId: string;
}

function getTokenFromCookie(formId: string): string | null {
    const cookieValue = document.cookie
        .split('; ')
        .find(row => row.startsWith("FPSK="))
        ?.split('=')[1];

    if (!cookieValue) return null;

    try {
        const tokens = JSON.parse(decodeURIComponent(cookieValue));
        return tokens[formId] || null;
    } catch {
        return null;
    }
}

async function fetchPartialResponse(formId: string, token: string) {
    try {
        const response = await fetch(`https://localhost:5001/api/forms/${formId}/submissions/by-token/${token}`);
        if (!response.ok) {
            console.warn('Failed to fetch partial response');
            return null;
        }

        var submissionData = await response.json();
        return submissionData;
    } catch (error) {
        console.error('Error fetching partial response:', error);
        return null;
    }
}

const SurveyJsWrapper = ({ formId, definition }: SurveyJsWrapperProps) => {
    const [partialResponse, setPartialResponse] = useState(null);

    useEffect(() => {
        const tokenValue = getTokenFromCookie(formId);

        if (tokenValue) {
            fetchPartialResponse(formId, tokenValue)
            .then(response => setPartialResponse(response));
        }
    }, []);

    return (
        <SurveyComponent
            formId={formId}
            definition={definition}
            data={partialResponse}
        />);
}

export default SurveyJsWrapper;