import { useMemo, useEffect } from "react";
import { ComponentCollection, Model } from "survey-core";
import { Submission } from "@/types";

ComponentCollection.Instance.add({
  name: 'video',
  title: 'Video',
  iconName: 'icon-preview-24x24',
  defaultQuestionTitle: 'Video',
  questionJSON: {
    type: 'file',
    title: 'Video Upload',
    description: 'Upload existing video ',
    acceptedTypes: 'video/*',
    storeDataAsText: false,
    waitForUpload: true,
    maxSize: 150000000,
    needConfirmRemoveFile: true,
    fileOrPhotoPlaceholder:
      'Drag and drop or select a video file to upload. Up to 150 MB',
    filePlaceholder: 'Drag and drop a video file or click "Select File"',
  },
  inheritBaseProps: true,
});

export function useSurveyModel(definition: string, submission?: Submission) {
  // Create survey model only when definition changes
  const surveyModel = useMemo(() => {
    return new Model(definition);
  }, [definition]);

  // Handle submission updates via effect
  useEffect(() => {
    if (submission) {
      try {
        surveyModel.data = JSON.parse(submission.jsonData);
        surveyModel.currentPageNo = submission.currentPage ?? 0;
      } catch (error) {
        console.debug("Failed to parse submission data", error);
      }
    }
  }, [submission, surveyModel]);

  return surveyModel;
}
