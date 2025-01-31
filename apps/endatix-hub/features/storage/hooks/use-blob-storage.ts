import { useEffect } from 'react';
import { SurveyModel, UploadFilesEvent } from 'survey-core';

interface UseBlobStorageProps {
  formId: string;
  submissionId?: string;
  surveyModel: SurveyModel | null;
  onSubmissionIdChange?: (newSubmissionId: string) => void;
}

interface UploadedFile {
  name: string;
  url: string;
}

export function useBlobStorage({
  formId,
  submissionId = '',
  onSubmissionIdChange,
  surveyModel,
}: UseBlobStorageProps) {
  const uploadFiles = async (
    sender: SurveyModel,
    options: UploadFilesEvent
  ) => {
    try {
      const formData = new FormData();
      options.files.forEach((file) => {
        formData.append(file.name, file);
      });

      const response = await fetch('/api/public/v0/storage/upload', {
        method: 'POST',
        body: formData,
        headers: {
          'edx-form-id': formId,
          'edx-submission-id': submissionId,
        },
      });

      const data = await response.json();

      if (!response.ok) {
        throw new Error(
          data?.error ??
            'Failed to upload files. Please refresh your page and try again.'
        );
      }

      if (data.submissionId && data.submissionId !== submissionId) {
        onSubmissionIdChange?.(data.submissionId);
      }

      const uploadedFiles = options.files.map((file) => {
        const remoteFile = data.files?.find(
          (uploadedFile: UploadedFile) => uploadedFile.name === file.name
        );
        return {
          file: file,
          content: remoteFile?.url,
        };
      });

      options.callback(uploadedFiles);
    } catch (error) {
      console.error('Error: ', error);
      options.callback([], [error instanceof Error ? error.message : '']);
    }
  };
  
  useEffect(() => {
    if (surveyModel) {
      surveyModel.onUploadFiles.add(uploadFiles);
      return () => {
        surveyModel.onUploadFiles.remove(uploadFiles);
      };
    }
  }, [surveyModel, uploadFiles]);

  return { uploadFiles };
}
