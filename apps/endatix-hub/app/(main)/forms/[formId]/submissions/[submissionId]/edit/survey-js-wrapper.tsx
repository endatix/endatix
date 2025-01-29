import { Submission } from '@/types';
import { useEffect, useMemo } from 'react';
import { ValueChangedEvent } from 'survey-core';
import { Model, Survey, SurveyModel } from 'survey-react-ui';

interface SurveyJsWrapperProps {
  submission: Submission;
  onChange: (sender: SurveyModel, event: ValueChangedEvent) => void;
}

function SurveyJsWrapper({ submission, onChange }: SurveyJsWrapperProps) {
  const surveyModel = useMemo(() => {
    if (!submission.formDefinition) {
      return null;
    }

    const json = JSON.parse(submission.formDefinition.jsonData);
    const model = new Model(json);

    model.showCompletedPage = false;
    model.validationEnabled = false;
    model.showPageTitles = false;
    model.showPageNumbers = false;
    model.showCompletedPage = false;
    model.showTitle = false;
    model.showProgressBar = 'off';
    model.validationEnabled = false;

    model.getAllPanels().forEach((panel) => {
      panel.expand();
    });

    let submissionData = {};
    try {
      submissionData = JSON.parse(submission?.jsonData);
    } catch (ex) {
      console.warn("Error while parsing submission's JSON data", ex);
    }

    model.data = submissionData;

    return model;
  }, [submission?.formDefinition, submission?.jsonData]);

  useEffect(() => {
    if (!surveyModel) {
      return;
    }

    surveyModel.onValueChanged.add(onChange);
    return () => {
      surveyModel.onValueChanged.remove(onChange);
    };
  }, [surveyModel, onChange]);

  if (!surveyModel) {
    return <div>Submission not found</div>;
  }

  return <Survey model={surveyModel} />;
}

export default SurveyJsWrapper;
