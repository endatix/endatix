import { BackToSubmissionsButton } from "./back-to-submissions-button";

interface SubmissionTopNavProps {
    formId: string;
}

export function SubmissionTopNav({
    formId
}: SubmissionTopNavProps) {
    return (
        <div className="sticky top-0 z-50 w-full border-b border-border/40 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 dark:border-border">
            <BackToSubmissionsButton
                className="h-14 py-4"
                formId={formId}
                text="Back to submissions"
                variant="link"
            />
        </div>
    )
}