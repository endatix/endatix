import { NextRequest, NextResponse } from "next/server";
import { SubmissionDataPdf } from "@/components/export/submission-data-pdf";
import { getSubmissionDetailsUseCase } from "@/features/submissions/use-cases/get-submission-details.use-case";
import { Result } from "@/lib/result";
import { pdf } from "@react-pdf/renderer";

type Params = {
  params: Promise<{
    formId: string;
    submissionId: string;
  }>;
};

const INLINE_QUERY_PARAM = "inline";
export async function GET(req: NextRequest, { params }: Params) {
  const { formId, submissionId } = await params;

  const searchParams = req.nextUrl.searchParams;
  const inline = searchParams.get(INLINE_QUERY_PARAM);

  const submissionResult = await getSubmissionDetailsUseCase({
    formId,
    submissionId,
  });
  if (Result.isError(submissionResult)) {
    return NextResponse.json(
      { error: "Submission not found" },
      { status: 404 },
    );
  }
  const submission = submissionResult.value;

  const pdfBlob = await pdf(
    <SubmissionDataPdf submission={submission} />,
  ).toBlob();

  const contentDisposition = inline === "true" ? "inline" : "attachment";

  return new Response(pdfBlob, {
    status: 200,
    headers: {
      "Content-Type": "application/pdf",
      "Content-Disposition": `${contentDisposition}; filename="submission-${submissionId}.pdf"`,
    },
  });
}
