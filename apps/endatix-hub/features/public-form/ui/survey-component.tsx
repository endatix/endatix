'use client'

import { SurveyModel, UploadFilesEvent } from 'survey-core'
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
      const result = await submitFormAction(formId, submissionData);
      if (Result.isError(result)) {
        console.debug('Failed to submit form', result.message);
      }
    });
  }, []);

  const uploadFiles = useCallback((_: SurveyModel, options: UploadFilesEvent) => {
    const formData = new FormData();
    options.files.forEach((file) => {
      formData.append(file.name, file);
    });

    fetch("/api/public/v0/storage/upload",
      {
        method: "POST",
        body: formData,
        headers: {
          "edx-form-id": formId,
        },
      })
      .then((response) => response.json())
      .then((data) => {
        options.callback(
          options.files.map((file) => {
            return {
              file: file,
              content: data.files.find((f: { name: string; url: string }) => f.name === file.name)?.url
            };
          })
        );
      })
      .catch((error) => {
        console.error("Error: ", error);
        options.callback([], ['An error occurred during file upload.']);
      });
  }, [formId]);

  model.onComplete.add(submitForm);
  model.onUploadFiles.add(uploadFiles);

  return (
    <Survey
      model={model}
      onComplete={submitForm}
      onValueChanged={updatePartial}
      onCurrentPageChanged={updatePartial}
      onDynamicPanelItemValueChanged={updatePartial}
      onMatrixCellValueChanged={updatePartial}
    />
  )
}
