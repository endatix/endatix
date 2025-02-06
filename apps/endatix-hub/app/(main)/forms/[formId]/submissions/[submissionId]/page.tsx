import { SubmissionTopNav } from "@/features/submissions/ui/details/submission-top-nav";
import { SectionTitle } from "@/components/headings/section-title";
import { Suspense } from "react";
import { Skeleton } from "@/components/ui/skeleton";
import SubmissionDetails from "@/features/submissions/ui/details/submission-details";
import { PdfEmbedView } from "@/features/pdf-export/components/pdf-embed-view";

type Params = {
  params: Promise<{
    formId: string;
    submissionId: string;
  }>;
  searchParams: Promise<{
    format: string;
  }>;
};

export default async function SubmissionPage({ params, searchParams }: Params) {
  const { formId, submissionId } = await params;
  const { format } = await searchParams;

  if (format?.toLowerCase() === "pdf") {
    return (
      <Suspense fallback={<SubmissionDataSkeleton />}>
        <PdfEmbedView formId={formId} submissionId={submissionId} />
      </Suspense>
    );
  }

  return (
    <>
      <SubmissionTopNav formId={formId} />
      <Suspense fallback={<SubmissionDataSkeleton />}>
        <SubmissionDetails formId={formId} submissionId={submissionId} />
      </Suspense>
    </>
  );
}

function SubmissionDataSkeleton() {
  const summaryQuestions = Array.from({ length: 5 }, (_, index) => index + 1);
  const answersQuestions = Array.from({ length: 16 }, (_, index) => index + 1);

  return (
    <div className="w-full overflow-auto">
      <div className="flex flex-col space-y-2 items-center">
        {summaryQuestions.map((question) => (
          <Skeleton className="h-8 w-[300px] " key={question} />
        ))}
      </div>
      <SectionTitle title="Submission Answers" headingClassName="py-2 my-0" />
      <div className="flex flex-col items-center space-y-2 items-center">
        {answersQuestions.map((question) => (
          <Skeleton className="h-12 w-[600px]" key={question} />
        ))}
      </div>
    </div>
  );
}
