'use client'

import { SurveyModel } from 'survey-core'
import { Survey } from 'survey-react-ui'
import { startTransition, useTransition, useCallback } from "react"
import { useSubmissionQueue } from '../application/submission-queue'
import { Result } from '@/lib/result'
import { Submission } from '@/types'
import { useSurveyModel } from './hooks/use-survey-model'
import { SubmissionData, submitFormAction } from '../application/actions/submit-form.action'

interface SurveyComponentProps {
  definition: string;
  formId: string;
  submission?: Submission;
}

export default function SurveyComponent({ definition, formId, submission }: SurveyComponentProps) {
  const [isSubmitting, startSubmitting] = useTransition();
  const model = useSurveyModel(definition, submission);
  const { enqueueSubmission, clearQueue } = useSubmissionQueue(formId);

  const updatePartial = useCallback((sender: SurveyModel) => {
    if (isSubmitting) {
      return;
    }

    const formData = JSON.stringify(sender.data, null, 3);
    const submissionData: SubmissionData = {
      isComplete: false,
      jsonData: formData,
      currentPage: sender.currentPageNo ?? 0
    }

    startTransition(() => {
      enqueueSubmission(submissionData);
    });
  }, []);

  const submitForm = useCallback((sender: SurveyModel) => {
    if (isSubmitting) {
      return;
    }
    clearQueue();
    const formData = JSON.stringify(sender.data, null, 3);

    const submissionData: SubmissionData = {
      isComplete: true,
      jsonData: formData,
      currentPage: sender.currentPageNo ?? 0
    }

    startSubmitting(async () => {
      var result = await submitFormAction(formId, submissionData);
      if (Result.isError(result)) {
        console.debug('Failed to submit form', result.message);
      }
    });
  }, []);

  model.onComplete.add(submitForm);

  return (
    <Survey
      model={model}
      onValueChanged={updatePartial}
      onCurrentPageChanged={updatePartial}
      onDynamicPanelItemValueChanged={updatePartial}
      onMatrixCellValueChanged={updatePartial}
    />
  )
}
