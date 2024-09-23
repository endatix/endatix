'use client'

import { Model } from 'survey-core'
import { Survey } from 'survey-react-ui'
import 'survey-core/defaultV2.css'
// import { json } from '../../data/survey_json.js'
  
export default function SurveyComponent() {
  const model = new Model("{}");
  return (
    <Survey model={model}/>
  );
}