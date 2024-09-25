'use client'

import { Model } from 'survey-core'
import { Survey } from 'survey-react-ui'
import 'survey-core/defaultV2.css'
import { submitForm } from '@/services/api';
  
interface SurveyComponentProps {
    definition: string;
    formId: string;
  }

export default async function SurveyComponent({ definition, formId }: SurveyComponentProps) { 
    const model = new Model(definition);
    
    const onFormComplete = async (sender: any, options: any) => {
      const formData = JSON.stringify(sender.data, null, 3);
      const metadata = {
        notes: "",
      };
      const submissionData = {
        jsonData: formData,
        isComplete: true,
        metadata: JSON.stringify(metadata, null, 3),
      };


      debugger
      const responses = await submitForm(formId, submissionData);
  }
  return <Survey model={model} onComplete={onFormComplete} />;
}