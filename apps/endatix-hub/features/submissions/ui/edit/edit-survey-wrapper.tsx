import { useBlobStorage } from '@/features/storage/hooks/use-blob-storage';
import { registerSpecializedQuestion, SpecializedVideo } from '@/lib/questions';
import { Submission } from '@/types';
import { useEffect, useRef } from 'react';
import { DynamicPanelItemValueChangedEvent, MatrixCellValueChangedEvent, ValueChangedEvent } from 'survey-core';
import { Model, Survey, SurveyModel } from 'survey-react-ui';

registerSpecializedQuestion(SpecializedVideo);

interface EditSurveyWrapperProps {
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

function EditSurveyWrapper({ 
  submission, 
  onChange,
}: EditSurveyWrapperProps) {
  const model = useSurveyModel(submission);

  useBlobStorage({
    formId: submission.formId,
    submissionId: submission.id,
    surveyModel: model,
  });

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

export default EditSurveyWrapper;
