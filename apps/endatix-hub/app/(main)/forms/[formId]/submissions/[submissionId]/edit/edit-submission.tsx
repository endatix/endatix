'use client';

import { Submission } from '@/types';
import 'survey-core/defaultV2.css';
import dynamic from 'next/dynamic';
import { SurveyModel, ValueChangedEvent } from 'survey-core';
import { useCallback, useState } from 'react';
import { Button } from '@/components/ui/button';
import { useRouter } from 'next/navigation';

const SurveyJsWrapper = dynamic(() => import('./survey-js-wrapper'), {
  ssr: false,
});

interface EditSubmissionProps {
  submission: Submission;
}

export default function EditSubmission({ submission }: EditSubmissionProps) {
  const [changes, setChanges] = useState<Record<string, unknown>>({});
  const [isSaving, setIsSaving] = useState(false);
  const router = useRouter();

  const onSubmissionChange = useCallback(
    (sender: SurveyModel, event: ValueChangedEvent) => {
      setChanges((prev) => ({
        ...prev,
        [event.name]: event.value,
      }));
    },
    []
  );

  const handleSave = useCallback(async () => {
    if (Object.keys(changes).length === 0) return;

    setIsSaving(true);
    try {
      setChanges({});
    } finally {
      setIsSaving(false);
    }
  }, [changes]);

  const handleDiscard = useCallback(() => {
    setChanges({});
    router.back();
  }, [router]);

  return (
    <div className="flex flex-col gap-4">
      <EditSubmissionHeader
        submission={submission}
        onSave={handleSave}
        onDiscard={handleDiscard}
        hasChanges={Object.keys(changes).length > 0}
        isSaving={isSaving}
      />
      <SurveyJsWrapper submission={submission} onChange={onSubmissionChange} />
    </div>
  );
}

interface EditSubmissionHeaderProps {
  submission: Submission;
  onSave: () => Promise<void>;
  onDiscard: () => void;
  hasChanges: boolean;
  isSaving: boolean;
}

function EditSubmissionHeader({
  submission,
  onSave,
  onDiscard,
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
          <Button variant="outline" onClick={onDiscard} disabled={isSaving}>
            Discard
          </Button>
          <Button
            variant="default"
            onClick={onSave}
            disabled={!hasChanges || isSaving}
          >
            {isSaving ? 'Saving...' : 'Save Changes'}
          </Button>
        </div>
      </div>
    </div>
  );
}
