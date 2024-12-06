'use client'

import { Model, SurveyModel } from 'survey-core'
import { Survey } from 'survey-react-ui'
import { useMemo, startTransition, useTransition, useCallback } from "react"
import { updateQueue } from '../lib/update-queue'
import { SubmissionData, submitFormAction } from '../submit-form.action'
import { Result } from '@/lib/result'

interface SurveyComponentProps {
  definition: string;
  formId: string;
}

export default function SurveyComponent({ definition, formId }: SurveyComponentProps) {
  const [isSubmitting, startSubmitting] = useTransition();
  const model = useMemo(() => new Model(definition), [definition]);

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
      updateQueue.enqueue({
        formId,
        data: submissionData,
      });
    });
  }, []);

  const submitForm = useCallback((sender: SurveyModel) => {
    if (isSubmitting) {
      return;
    }
    updateQueue.clear();
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
