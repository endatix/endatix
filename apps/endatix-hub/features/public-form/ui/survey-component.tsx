"use client";

import { CompleteEvent, SurveyModel } from "survey-core";
import { Survey } from "survey-react-ui";
import { useTransition, useCallback, useState, useEffect } from "react";
import { useSubmissionQueue } from "../application/submission-queue";
import { Result } from "@/lib/result";
import { Submission } from "@/types";
import "survey-core/defaultV2.css";
import { useSurveyModel } from "./use-survey-model.hook";
import {
  SubmissionData,
  submitFormAction,
} from "@/features/public-form/application/actions/submit-form.action";
import { useBlobStorage } from "@/features/storage/hooks/use-blob-storage";

interface SurveyComponentProps {
  definition: string;
  formId: string;
  submission?: Submission;
}

export default function SurveyComponent({
  definition,
  formId,
  submission,
}: SurveyComponentProps) {
  const model = useSurveyModel(definition, submission);
  const { enqueueSubmission, clearQueue } = useSubmissionQueue(formId);
  const [isSubmitting, startSubmitting] = useTransition();
  const [submissionId, setSubmissionId] = useState<string>(
    submission?.id ?? "",
  );

  useBlobStorage({
    formId,
    submissionId,
    surveyModel: model,
    onSubmissionIdChange: setSubmissionId,
  });

  useEffect(() => {
    if (submission?.id) {
      setSubmissionId(submission.id);
    }
  }, [submission?.id]);

  const updatePartial = useCallback(
    (sender: SurveyModel) => {
      const formData = JSON.stringify(sender.data, null, 3);
      const submissionData: SubmissionData = {
        isComplete: false,
        jsonData: formData,
        currentPage: sender.currentPageNo,
      };

      enqueueSubmission(submissionData);
    },
    [enqueueSubmission],
  );

  const submitForm = useCallback(
    (sender: SurveyModel, event: CompleteEvent) => {
      if (isSubmitting) {
        return;
      }

      clearQueue();
      event.showSaveInProgress();
      const formData = JSON.stringify(sender.data, null, 3);

      const submissionData: SubmissionData = {
        isComplete: true,
        jsonData: formData,
        currentPage: sender.currentPageNo ?? 0,
      };

      startSubmitting(async () => {
        const result = await submitFormAction(formId, submissionData);
        if (Result.isSuccess(result)) {
          event.showSaveSuccess("The results were saved successfully!");
        } else {
          event.showSaveError(
            "Failed to submit form. Please try again and contact us if the problem persists.",
          );
          console.debug("Failed to submit form", result.message);
        }
      });
    },
    [formId, isSubmitting, clearQueue, startSubmitting],
  );

  useEffect(() => {
    model.onComplete.add(submitForm);
    model.onValueChanged.add(updatePartial);
    model.onCurrentPageChanged.add(updatePartial);
    model.onDynamicPanelItemValueChanged.add(updatePartial);
    model.onMatrixCellValueChanged.add(updatePartial);

    return () => {
      model.onComplete.remove(submitForm);
      model.onValueChanged.remove(updatePartial);
      model.onCurrentPageChanged.remove(updatePartial);
      model.onDynamicPanelItemValueChanged.remove(updatePartial);
      model.onMatrixCellValueChanged.remove(updatePartial);
    };
  }, [model, submitForm, updatePartial]);

  return <Survey model={model} />;
}
