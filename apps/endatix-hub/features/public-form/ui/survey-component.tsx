"use client";

import { CompleteEvent, SurveyModel, UploadFilesEvent } from "survey-core";
import { Survey } from "survey-react-ui";
import { useTransition, useCallback, useState, useEffect } from "react";
import { useSubmissionQueue } from "../application/submission-queue";
import { Result } from "@/lib/result";
import { Submission } from "@/types";
import "survey-core/defaultV2.css";
import { useSurveyModel } from "./hooks/use-survey-model";
import {
  SubmissionData,
  submitFormAction,
} from "@/features/public-form/application/actions/submit-form.action";

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
    submission?.id ?? ""
  );

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
    [enqueueSubmission]
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
            "Failed to submit form. Please try again and contact us if the problem persists."
          );
          console.debug("Failed to submit form", result.message);
        }
      });
    },
    [formId, isSubmitting, clearQueue, startSubmitting]
  );

  const uploadFiles = useCallback(
    async (_: SurveyModel, options: UploadFilesEvent) => {
      try {
        const formData = new FormData();
        options.files.forEach((file) => {
          formData.append(file.name, file);
        });
        const response = await fetch("/api/public/v0/storage/upload", {
          method: "POST",
          body: formData,
          headers: {
            "edx-form-id": formId,
            "edx-submission-id": submissionId ?? "",
          },
        });
        const data = await response.json();

        if (!response.ok) {
          throw new Error(data?.error ?? "Failed to upload files. Please refresh your page and try again.");
        }

        if (data.submissionId && data.submissionId !== submissionId) {
          setSubmissionId(data.submissionId);
        }

        const uploadedFiles = options.files.map((file) => {
          const remoteFile = data.files?.find(
            (uploadedFile: { name: string; url: string }) =>
              uploadedFile.name === file.name
          );
          return {
            file: file,
            content: remoteFile?.url,
          };
        });
        options.callback(uploadedFiles);
      } catch (error) {
        console.error("Error: ", error);
        options.callback([], [error instanceof Error ? error.message : ""]);
      }
    },
    [formId, submissionId]
  );

  model.onComplete.add(submitForm);
  model.onUploadFiles.add(uploadFiles);
  model.onValueChanged.add(updatePartial);
  model.onCurrentPageChanged.add(updatePartial);
  model.onDynamicPanelItemValueChanged.add(updatePartial);
  model.onMatrixCellValueChanged.add(updatePartial);

  return <Survey model={model} />;
}
