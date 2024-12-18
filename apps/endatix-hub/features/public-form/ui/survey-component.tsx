'use client'

import { Question, SurveyModel, ValueChangingEvent } from 'survey-core'
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

const MAX_IMAGE_SIZE: number = Number(process.env.NEXT_PUBLIC_MAX_IMAGE_SIZE) || 0;

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

  const interceptImages = useCallback((sender: SurveyModel, options: ValueChangingEvent) => {
    const resizeImage = (imgSrc: string, type: string): Promise<string> => {
      return new Promise((resolve) => {
        const img = new Image();
        img.onload = () => {
          let width = img.width;
          let height = img.height;
    
          if (width > height && width > MAX_IMAGE_SIZE) {
              height = height * (MAX_IMAGE_SIZE / width);
              width = MAX_IMAGE_SIZE;
          } else if (height > MAX_IMAGE_SIZE) {
              width = width * (MAX_IMAGE_SIZE / height);
              height = MAX_IMAGE_SIZE;
          }
    
          let canvas = document.createElement("canvas");
          canvas.width = width;
          canvas.height = height;
          const ctx = canvas.getContext("2d");
    
          if (ctx) {
            ctx.drawImage(img, 0, 0, width, height);
            resolve(canvas.toDataURL(type));
          }
        };
        img.src = imgSrc;
      });
    };
    
    const processImages = async () => {
    
      for (let i = 0; i < options.question.value.length; i++) {
        let value = options.value[i];
        if (value?.type?.includes('image')) {
          value.content = await resizeImage(
            value.content,
            value.type
            );
        }
      }
    };
    
    if(MAX_IMAGE_SIZE > 0 && options.question.getType() == "file") {
      processImages();
    }
       
  }, []);

  model.onComplete.add(submitForm);
  model.onValueChanging.add(interceptImages);

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
