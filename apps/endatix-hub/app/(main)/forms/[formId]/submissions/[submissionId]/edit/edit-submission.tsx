'use client';

import { Submission } from '@/types';
import 'survey-core/defaultV2.css';
import dynamic from 'next/dynamic';
import { Question, SurveyModel, ValueChangedEvent } from 'survey-core';
import { useCallback, useMemo, useState, useTransition } from 'react';
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
import {
  FileIcon,
  Info,
  MessageSquareTextIcon,
  PencilLine,
} from 'lucide-react';
import { Spinner } from '@/components/loaders/spinner';
import { toast } from 'sonner';
import { editSubmissionUseCase } from '@/features/submissions/use-cases/edit-submission.use-case';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { Separator } from '@/components/ui/separator';

const SurveyJsWrapper = dynamic(() => import('./survey-js-wrapper'), {
  ssr: false,
});

interface EditSubmissionProps {
  submission: Submission;
}

export default function EditSubmission({ submission }: EditSubmissionProps) {
  const submissionData: Record<string, any> = useMemo(() => {
    try {
      return JSON.parse(submission.jsonData);
    } catch (error) {
      return {};
    }
  }, [submission.jsonData]);
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);
  const [discardDialogOpen, setDiscardDialogOpen] = useState(false);
  const [changes, setChanges] = useState<Record<string, Question>>({});
  const [surveyModel, setSurveyModel] = useState<SurveyModel | null>(null);
  const [isPending, startTransition] = useTransition();
  const router = useRouter();

  const onSubmissionChange = useCallback(
    (sender: SurveyModel, event: ValueChangedEvent) => {
      const originalQuestionValue = submissionData[event.name];
      const newQuestionValue = event.question?.value;
      if (originalQuestionValue !== newQuestionValue) {
        setChanges((prev) => ({
          ...prev,
          [event.name]: event.question,
        }));
      } else {
        setChanges((prev) => {
          const newChanges = { ...prev };
          delete newChanges[event.name];
          return newChanges;
        });
      }

      setSurveyModel(sender);
    },
    []
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
  changes: Record<string, Question>;
  isSaving: boolean;
  onAction: (event: React.FormEvent<HTMLButtonElement>) => Promise<void>;
}

function EditSubmissionAlertDialog({
  submission,
  changes,
  onAction,
  isSaving,
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
                <li className="mb-4" key={key}>
                  <QuestionValueDisplay question={changes[key]} />
                </li>
              ))}
            </ul>
          </div>
          <AlertDialogDescription>
            Click "Save Changes" to confirm and save your changes or dismiss to
            continue editing.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>
            <PencilLine className="h-4 w-4" />
            Continue Editing
          </AlertDialogCancel>
          <AlertDialogAction onClick={onAction} disabled={isSaving}>
            {isSaving && <Spinner className="h-4 w-4" />}
            {isSaving ? 'Saving changes...' : 'Yes, save changes'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

const QuestionComment = ({ comment }: { comment: string }) => {
  return (
    <div className="flex flex-row items-start gap-2">
      <MessageSquareTextIcon className="h-4 w-4 text-muted-foreground" />
      <span className="text-muted-foreground text-left">{comment}</span>
    </div>
  );
};

const QuestionValueDisplay = ({ question }: { question: Question }) => {
  if (!question) return null;

  const QuestionWrapper = ({ children }: { children: React.ReactNode }) => (
    <div className="flex flex-row items-start gap-2 text-sm">
      <div className="flex flex-row items-center justify-end gap-2 w-1/2">
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Info className="h-4 w-4 hidden md:block" />
            </TooltipTrigger>
            <TooltipContent>{question.title}</TooltipContent>
          </Tooltip>
        </TooltipProvider>
        <span className="font-medium">{question.name} :</span>
      </div>
      <div className="flex flex-col items-start gap-0">
        {children}
        {question.hasComment && <QuestionComment comment={question.comment} />}
      </div>
    </div>
  );

  const renderValue = () => {
    switch (question.getType()) {
      case 'text':
      case 'comment':
      case 'dropdown':
        return <div className="text-muted-foreground">{question.value}</div>;

      case 'select':
        return (
          <div className="text-muted-foreground">
            {Array.isArray(question.value)
              ? question.value.join(', ')
              : question.value}
          </div>
        );

      case 'boolean':
      case 'checkbox':
        return (
          <span className="text-muted-foreground">
            {question.value ? 'Yes' : 'No'}
          </span>
        );

      case 'file':
        return (
          <div className="flex flex-row items-start gap-2">
            <FileIcon className="h-4 w-4" />
            <span className="text-muted-foreground">
              has {question.value.length}{' '}
              {question.value.length === 1 ? 'file' : 'files'}
            </span>
          </div>
        );

      default:
        return (
          <span className="text-muted-foreground">
            {String(question.value)}
          </span>
        );
    }
  };

  return <QuestionWrapper>{renderValue()}</QuestionWrapper>;
};
