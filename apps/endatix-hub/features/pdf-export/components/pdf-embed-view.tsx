'use server';

interface PdfEmbedViewProps {
  formId: string;
  submissionId: string;
}

export async function PdfEmbedView({
  formId,
  submissionId,
}: PdfEmbedViewProps) {
  const pdfUrl = `/api/public/v0/forms/${formId}/submissions/${submissionId}/export-pdf?inline=true`;

  return (
    <div className="flex justify-center items-center h-screen">
      <embed
        src={pdfUrl}
        type="application/pdf"
        width="100%"
        height="100%"
        style={{ border: 'none' }}
        title="Submission PDF Viewer"
      />
    </div>
  );
}
