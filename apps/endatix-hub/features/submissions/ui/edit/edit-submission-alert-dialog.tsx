import { Spinner } from "@/components/loaders/spinner";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { AlertDialogProps } from "@radix-ui/react-alert-dialog";
import { PencilLine } from "lucide-react";
import ChangedQuestion from "./changed-question";
import { Submission } from "@/types";
import { Question } from "survey-core";

interface EditSubmissionAlertDialogProps extends AlertDialogProps {
  submission: Submission;
  changes: Record<string, Question>;
  isSaving: boolean;
  onAction: (event: React.FormEvent<HTMLButtonElement>) => Promise<void>;
}

function EditSubmissionAlertDialog({
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
                  <ChangedQuestion question={changes[key]} />
                </li>
              ))}
            </ul>
          </div>
          <AlertDialogDescription>
            Click &quot;Save Changes&quot; to confirm and save your changes or
            dismiss to continue editing.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>
            <PencilLine className="h-4 w-4" />
            Continue Editing
          </AlertDialogCancel>
          <AlertDialogAction onClick={onAction} disabled={isSaving}>
            {isSaving && <Spinner className="h-4 w-4" />}
            {isSaving ? "Saving changes..." : "Yes, save changes"}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

export default EditSubmissionAlertDialog;
