'use client'

import { Model } from 'survey-core'
import { Survey } from 'survey-react-ui'
import 'survey-core/defaultV2.css'
  
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
      const requestBody = {
        jsonData: formData,
        isComplete: true,
        metadata: JSON.stringify(metadata, null, 3),
      };

      try {
        console.log('https://localhost:5001/api/forms/' + formId + '/submissions');
        const response = await fetch('https://localhost:5001/api/forms/' + formId + '/submissions', {
          method: 'POST',
          mode: 'no-cors',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(requestBody),
        });
  
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
  
        const submission = await response.json();
        console.log('Form data submitted successfully:', submission);
      } catch (error: any) {
        console.error('Failed to submit form data:', error.message);
      }

      const surveyResults = sender.data;
      console.log('Survey completed!', surveyResults);
    };

    return (
     <Survey model={model} onComplete={onFormComplete} />
    );
  }
  