import { NextResponse } from "next/server";
import { SubmissionDataPdf } from "@/components/export/submission-data-pdf";
import { getSubmissionDetailsUseCase } from "@/features/submissions/use-cases/get-submission-details.use-case";
import { Result } from "@/lib/result";
import { pdf } from "@react-pdf/renderer";

type Params = {
  params: Promise<{
    formId: string;
    submissionId: string;
  }>
}

export async function GET(req: Request, { params }: Params) {
  const { formId, submissionId } = await params;

  const submissionResult = await getSubmissionDetailsUseCase({ formId, submissionId });
  if (Result.isError(submissionResult)) {
    return NextResponse.json({ error: "Submission not found" }, { status: 404 });
  }
  const submission = submissionResult.value;

  const pdfBlob = await pdf(<SubmissionDataPdf submission={submission} />).toBlob();

  return new Response(pdfBlob, {
    status: 200,
    headers: {
      "Content-Type": "application/pdf",
      "Content-Disposition": `attachment; filename=submission-${submissionId}.pdf`,
    },
  });
}
