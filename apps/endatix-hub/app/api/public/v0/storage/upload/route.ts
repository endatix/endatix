import {
  SubmissionData,
  submitFormAction,
} from "@/features/public-form/application/actions/submit-form.action";
import { uploadUserFilesUseCase } from "@/features/storage/use-cases/upload-user-files.use-case";
import { Result } from "@/lib/result";
import { headers } from "next/headers";

type UploadUserFilesResult = {
  success: boolean;
  submissionId: string;
  files: {
    name: string;
    url: string;
  }[];
};

export async function POST(request: Request) {
  const requestHeaders = await headers();
  const formId = requestHeaders.get("edx-form-id") as string;
  let submissionId = requestHeaders.get("edx-submission-id") as string;
  const formData = await request.formData();

  if (!formId) {
    return Response.json({ error: "Form ID is required" }, { status: 400 });
  }

  if (!submissionId) {
    const submissionData: SubmissionData = {
      isComplete: false,
      jsonData: JSON.stringify({}),
      metadata: JSON.stringify({
        reasonCreated: "Generate submissionId for image upload",
      }),
    };
    const initialSubmissionResult = await submitFormAction(
      formId,
      submissionData
    );

    if (Result.isError(initialSubmissionResult)) {
      return Response.json(
        { error: initialSubmissionResult.message },
        { status: 400 }
      );
    }

    submissionId = initialSubmissionResult.value.submissionId;
  }

  const files: { name: string; file: File }[] = [];
  for (const [filename, file] of formData.entries()) {
    if (!file || typeof file === "string") {
      return Response.json(
        { error: `Invalid file for ${filename}` },
        { status: 400 }
      );
    }

    files.push({
      name: filename,
      file: file as File,
    });
  }

  if (files.length === 0) {
    return Response.json({ error: "No files provided" }, { status: 400 });
  }

  const result = await uploadUserFilesUseCase({
    formId: formId,
    submissionId: submissionId,
    files: files,
  });

  if (Result.isError(result)) {
    return Response.json({ error: result.message }, { status: 400 });
  }

  const uploadUserFilesResult: UploadUserFilesResult = {
    success: true,
    submissionId: submissionId,
    files: result.value,
  };

  return Response.json(uploadUserFilesResult);
}
