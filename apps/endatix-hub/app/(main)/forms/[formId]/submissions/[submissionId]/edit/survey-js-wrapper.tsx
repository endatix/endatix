import { Submission } from '@/types';
import { useEffect, useRef } from 'react';
import { DynamicPanelItemValueChangedEvent, MatrixCellValueChangedEvent, ValueChangedEvent } from 'survey-core';
import { Model, Survey, SurveyModel } from 'survey-react-ui';

interface SurveyJsWrapperProps {
  submission: Submission;
  onChange: (sender: SurveyModel, event: ValueChangedEvent | DynamicPanelItemValueChangedEvent | MatrixCellValueChangedEvent) => void;
}

function useSurveyModel(submission: Submission) {
  const modelRef = useRef<Model | null>(null);

  if (!modelRef.current) {
    if (!submission.formDefinition?.jsonData) {
      return null;
    }

    try {
      const json = JSON.parse(submission.formDefinition.jsonData);
      const submissionData = JSON.parse(submission.jsonData);
      const model = new Model(json);

      model.data = submissionData;
      model.showCompletedPage = false;
      model.validationEnabled = false;
      model.showPageTitles = false;
      model.showPageNumbers = false;
      model.showNavigationButtons = false;
      model.showTitle = false;
      model.showProgressBar = 'off' as const;
      model.getAllPanels().forEach((panel) => {
        panel.expand();
      });

      modelRef.current = model;
    } catch (error) {
      console.error('Error initializing survey model:', error);
      return null;
    }
  }

  return modelRef.current;
}

function SurveyJsWrapper({ 
  submission, 
  onChange,
}: SurveyJsWrapperProps) {
  const model = useSurveyModel(submission);

  useEffect(() => {
    if (!model) return;

    model.onValueChanged.add(onChange);
    model.onDynamicPanelItemValueChanged.add(onChange);
    model.onMatrixCellValueChanged.add(onChange);
    return () => {
      model.onValueChanged.remove(onChange);
      model.onDynamicPanelItemValueChanged.remove(onChange);
      model.onMatrixCellValueChanged.remove(onChange);
    };
  }, [model, onChange]);

  if (!model) {
    return <div>Submission not found</div>;
  }

  return <Survey model={model} />;
}

export default SurveyJsWrapper;
