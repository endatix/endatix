import { SubmissionStatusType } from '@/types';

export interface ChangeStatusCommand {
  submissionId: string;
  formId: string;
  status: SubmissionStatusType;
}

export interface ChangeStatusResult {
  success: boolean;
  error?: string;
} 