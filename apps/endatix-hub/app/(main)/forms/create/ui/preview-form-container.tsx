'use client';

import dynamic from 'next/dynamic';

const PreviewForm = dynamic(() => import('./preview-form'), {
    ssr: false,
});

interface PreviewFormContainerProps {
    model: string;
}

const PreviewFormContainer = ({ model }: PreviewFormContainerProps) => {
    return (
        <PreviewForm model={model} />
    )
}

export default PreviewFormContainer;