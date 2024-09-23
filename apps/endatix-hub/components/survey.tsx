'use client'

import { Model } from 'survey-core'
import { Survey } from 'survey-react-ui'
import 'survey-core/defaultV2.css'
  
interface SurveyComponentProps {
    definition: string;
  }

export default function SurveyComponent({ definition }: SurveyComponentProps) { 
    const model = new Model(definition);
    
    return (
     <Survey model={model} />
    );
  }
  