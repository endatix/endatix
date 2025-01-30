import { Spinner } from '@/components/loaders/spinner';
import { Button } from '@/components/ui/button';
import { Submission } from '@/types';

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

export default EditSubmissionHeader;
