'use client';

import { Submission } from '@/types';
import 'survey-core/defaultV2.css';
import dynamic from 'next/dynamic';
import {
  DynamicPanelItemValueChangedEvent,
  MatrixCellValueChangedEvent,
  Question,
  SurveyModel,
  ValueChangedEvent,
} from 'survey-core';
import { useCallback, useMemo, useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { Info } from 'lucide-react';
import { toast } from 'sonner';
import { editSubmissionUseCase } from '@/features/submissions/use-cases/edit-submission.use-case';
import EditSubmissionAlertDialog from './edit-submission-alert-dialog';
import EditSubmissionHeader from './edit-submission-header';

const EditSurveyWrapper = dynamic(() => import('./edit-survey-wrapper'), {
  ssr: false,
});

interface EditSubmissionProps {
  submission: Submission;
}

function EditSubmission({ submission }: EditSubmissionProps) {
  const submissionData: Record<string, unknown> = useMemo(() => {
    try {
      return JSON.parse(submission.jsonData);
    } catch {
      return {};
    }
  }, [submission.jsonData]);
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);
  const [changes, setChanges] = useState<Record<string, Question>>({});
  const [surveyModel, setSurveyModel] = useState<SurveyModel | null>(null);
  const [isPending, startTransition] = useTransition();
  const router = useRouter();

  const onSubmissionChange = useCallback(
    (
      sender: SurveyModel,
      event:
        | ValueChangedEvent
        | DynamicPanelItemValueChangedEvent
        | MatrixCellValueChangedEvent
    ) => {
      const originalQuestionValue = submissionData[event.question.name];
      const newQuestionValue = event.question?.value;
      if (originalQuestionValue !== newQuestionValue) {
        setChanges((prev) => ({
          ...prev,
          [event.question.name]: event.question,
        }));
      } else {
        setChanges((prev) => {
          const newChanges = { ...prev };
          delete newChanges[event.question.name];
          return newChanges;
        });
      }

      setSurveyModel(sender);
    },
    [submissionData]
  );

  const handleSave = useCallback(
    async (event: React.FormEvent<HTMLButtonElement>) => {
      event.preventDefault();
      if (!surveyModel?.data || Object.keys(changes).length === 0) return;

      try {
        startTransition(async () => {
          await editSubmissionUseCase(submission.formId, submission.id, {
            jsonData: JSON.stringify(surveyModel.data),
          });
          toast.success('Changes saved');
          setSaveDialogOpen(false);
          router.push(
            `/forms/${submission.formId}/submissions/${submission.id}`
          );
        });
      } catch (error) {
        console.error(error);
        toast.error('Failed to save changes');
      }
    },
    [changes]
  );

  const handleDiscard = useCallback(() => {
    if (isPending) {
      return;
    }
    setSaveDialogOpen(false);
    router.back();
  }, [router, isPending]);

  return (
    <div className="flex flex-col gap-4">
      <EditSubmissionHeader
        submission={submission}
        onSaveClick={() => setSaveDialogOpen(true)}
        onDiscardClick={handleDiscard}
        hasChanges={Object.keys(changes).length > 0}
        isSaving={isPending}
      />
      <EditSurveyWrapper
        submission={submission}
        onChange={onSubmissionChange}
      />
      <div className="h-8 text-muted-foreground flex flex-row justify-center items-center gap-2">
        <Info className="h-4 w-4" />
        End of submission
      </div>
      <EditSubmissionAlertDialog
        submission={submission}
        changes={changes}
        isSaving={isPending}
        open={saveDialogOpen}
        onAction={handleSave}
        onOpenChange={() => {
          if (isPending) {
            return;
          }
          setSaveDialogOpen(!saveDialogOpen);
        }}
      />
    </div>
  );
}

export default EditSubmission;
