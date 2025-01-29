'use client';

import { Submission } from '@/types';
import 'survey-core/defaultV2.css';
import dynamic from 'next/dynamic';
import { SurveyModel, ValueChangedEvent } from 'survey-core';
import { useCallback, useState, useTransition } from 'react';
import { Button } from '@/components/ui/button';
import { useRouter } from 'next/navigation';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { AlertDialogProps } from '@radix-ui/react-alert-dialog';
import { Link2, PencilLine } from 'lucide-react';
import { Spinner } from '@/components/loaders/spinner';
import { toast } from 'sonner';
import { editSubmissionUseCase } from '@/features/submissions/use-cases/edit-submission.use-case';

const SurveyJsWrapper = dynamic(() => import('./survey-js-wrapper'), {
  ssr: false,
});

interface EditSubmissionProps {
  submission: Submission;
}

export default function EditSubmission({ submission }: EditSubmissionProps) {
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);
  const [changes, setChanges] = useState<
    Record<string, string | number | boolean | object>
  >({});
  const [surveyModel, setSurveyModel] = useState<SurveyModel | null>(null);
  const [isPending, startTransition] = useTransition();
  const router = useRouter();

  const onSubmissionChange = useCallback(
    (sender: SurveyModel, event: ValueChangedEvent) => {
      setChanges((prev) => ({
        ...prev,
        [event.name]: event.value,
      }));
      setSurveyModel(sender);
    },
    []
  );

  const handleSave = useCallback(async () => {
    if (!surveyModel?.data || Object.keys(changes).length === 0) return;

    try {
      startTransition(async () => {
        await editSubmissionUseCase(submission.formId, submission.id, {
          jsonData: JSON.stringify(surveyModel.data),
        });
        setChanges({});
        setSaveDialogOpen(false);
        toast.success('Changes saved');
        router.refresh();
      });
    } catch (error) {
      console.error(error);
      toast.error('Failed to save changes');
    }
  }, [changes]);

  const handleDiscard = useCallback(() => {
    setSaveDialogOpen(false);
    setChanges({});
    router.back();
  }, [router]);

  return (
    <div className="flex flex-col gap-4">
      <EditSubmissionHeader
        submission={submission}
        onSaveClick={() => setSaveDialogOpen(true)}
        onDiscardClick={handleDiscard}
        hasChanges={Object.keys(changes).length > 0}
        isSaving={isPending}
      />
      <SurveyJsWrapper submission={submission} onChange={onSubmissionChange} />
      <EditSubmissionAlertDialog
        submission={submission}
        changes={changes}
        open={saveDialogOpen}
        onSave={handleSave}
        onOpenChange={() => setSaveDialogOpen(!saveDialogOpen)}
      />
    </div>
  );
}

interface EditSubmissionHeaderProps {
  submission: Submission;
  onSaveClick: () => void;
  onDiscardClick: () => void;
  hasChanges: boolean;
  isSaving: boolean;
}

function EditSubmissionHeader({
  submission,
  onSaveClick,
  onDiscardClick,
  hasChanges,
  isSaving,
}: EditSubmissionHeaderProps) {
  const formDefinition = JSON.parse(
    submission.formDefinition?.jsonData ?? '{}'
  );

  return (
    <div className="sticky top-0 py-4 z-50 w-full border-b border-border/40 bg-background/10 backdrop-blur supports-[backdrop-filter]:bg-background/30 hover:bg-background/95 transition-colors duration-200 dark:border-border">
      <div className="flex flex-col gap-0 text-center w-full md:w-1/2 mx-auto">
        <h1 className="text-2xl font-bold">{formDefinition.title}</h1>
        <p className="text-muted-foreground">
          Editing submission {submission.id}
        </p>
        <div className="flex flex-row gap-2 justify-end mt-4">
          <Button
            variant="outline"
            onClick={onDiscardClick}
            disabled={isSaving}
          >
            Discard
          </Button>
          <Button
            variant="default"
            onClick={onSaveClick}
            disabled={!hasChanges || isSaving}
          >
            {isSaving && <Spinner className="h-4 w-4" />}
            {isSaving ? 'Saving...' : 'Save Changes'}
          </Button>
        </div>
      </div>
    </div>
  );
}

interface EditSubmissionAlertDialogProps extends AlertDialogProps {
  submission: Submission;
  changes: Record<string, unknown>;
  onSave: () => Promise<void>;
}

function EditSubmissionAlertDialog({
  submission,
  changes,
  onSave,
  ...props
}: EditSubmissionAlertDialogProps) {
  return (
    <AlertDialog {...props}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle className="flex flex-row items-center gap-2">
            Please review the following changes to this submission
          </AlertDialogTitle>
          <div className="text-sm text-muted-foreground space-y-4 mb-1">
            <span className="text-sm">
              You have made the following changes:
            </span>
            <ul className="list-disclist-inside">
              {Object.keys(changes).map((key) => (
                <li key={key} className="text-sm text-muted-foreground flex flex-row items-center gap-2">
                  <Link2 className="h-4 w-4" />
                  <strong>{key}</strong>: {changes[key]?.toString()}
                </li>
              ))}
            </ul>
          </div>
          <AlertDialogDescription>
            Click "Save Changes" to confirm and save your changes or dismiss to continue editing.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>
            <PencilLine className="h-4 w-4" />
            Continue Editing
          </AlertDialogCancel>
          <AlertDialogAction onClick={onSave}>
            Yes, save changes
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
