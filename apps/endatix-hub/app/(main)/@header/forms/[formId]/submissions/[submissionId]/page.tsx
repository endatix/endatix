import MainHeader from "@/components/layout-ui/header/main-header";
import { SubmissionTopNav } from "@/features/submissions/ui/details/submission-top-nav";

type Params = {
  params: Promise<{
    formId: string;
    submissionId: string;
  }>;
};

export default async function SubmissionPageHeader({ params }: Params) {
  const { formId } = await params;
  return (
    <>
      <MainHeader showHeader={true} />
      <SubmissionTopNav formId={formId} />
    </>
  );
}
