'use client'

import { Model, SurveyModel } from 'survey-core'
import { Survey } from 'survey-react-ui'
import { useMemo, startTransition, useTransition, useCallback } from "react"
import { updateQueue } from '../lib/update-queue'
import { SubmissionData, submitFormAction } from '../submit-form.action'
import { Result } from '@/lib/result'
import { Submission } from '@/types'

interface SurveyComponentProps {
  definition: string;
  formId: string;
  submission?: Submission;
}

export default function SurveyComponent({ definition, formId, submission }: SurveyComponentProps) {
  const [isSubmitting, startSubmitting] = useTransition();
  const model = useMemo(() => {
    const surveyModel = new Model(definition);
    if (submission) {
      surveyModel.data = JSON.parse(submission.jsonData);
      surveyModel.currentPageNo = submission.currentPage;
    }
    return surveyModel;
  } , [definition, submission]);

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
