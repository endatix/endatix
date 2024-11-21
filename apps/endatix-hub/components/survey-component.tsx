'use client'

import { Model } from 'survey-core'
import { Survey } from 'survey-react-ui'
import 'survey-core/defaultV2.css'
import { startTransition } from "react";
import { submitFormAction } from '@/app/(public)/share/[formId]/submit-form.action';

interface SurveyComponentProps {
    definition: string;
    formId: string;
  }

export default function SurveyComponent({ definition, formId }: SurveyComponentProps) {
    const model = new Model(definition);

    const onFormComplete = async (sender: Model) => {
      const formData = JSON.stringify(sender.data, null, 3);
      const metadata = {
        notes: "",
      };
      const submissionData = {
        jsonData: formData,
        isComplete: true,
        metadata: JSON.stringify(metadata, null, 3),
      };
      startTransition(async () => {
        await submitFormAction(formId, submissionData);
      })
  }
  return <Survey model={model} onComplete={onFormComplete} />;
}
