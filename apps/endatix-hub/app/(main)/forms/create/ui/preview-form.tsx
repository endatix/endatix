"use client"

import { Model } from 'survey-core'
import { Survey } from 'survey-react-ui'
import 'survey-core/defaultV2.css'

interface PreviewFormProps {
    model: Model | null;
}

const PreviewForm = async ({ model }: PreviewFormProps) => {
    const onFormComplete = async (sender: Model) => {
        console.log(sender.data);
    }
    return (
        model && <Survey model={model} onComplete={onFormComplete} />
    );
}

export default PreviewForm; 