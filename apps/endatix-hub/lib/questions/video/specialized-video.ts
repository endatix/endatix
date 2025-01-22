import { ICustomQuestionTypeConfiguration } from 'survey-core';
import { SpecializedSurveyQuestion } from '../infrastructure/specialized-survey-question';

export class SpecializedVideo extends SpecializedSurveyQuestion {
  get customQuestionConfig(): ICustomQuestionTypeConfiguration {
    return {
      name: 'video',
      title: 'Video',
      iconName: 'icon-preview-24x24', 
      defaultQuestionTitle: 'Video',
      questionJSON: {
        type: 'file',
        title: 'Video Upload',
        description: 'Upload existing video',
        acceptedTypes: 'video/*',
        storeDataAsText: false,
        waitForUpload: true,
        maxSize: 150_000_000, // 150MB
        needConfirmRemoveFile: true,
        fileOrPhotoPlaceholder: 'Drag and drop or select a video file to upload. Up to 150 MB',
        filePlaceholder: 'Drag and drop a video file or click "Select File"'
      },
      inheritBaseProps: true
    };
  }
}