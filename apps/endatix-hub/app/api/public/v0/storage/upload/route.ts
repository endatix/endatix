import { uploadUserFilesUseCase } from "@/features/storage/use-cases/upload-user-files.use-case";
import { Result } from "@/lib/result";
import { headers } from "next/headers";

export async function POST(request: Request) {
  const requestHeaders = await headers();
  const formId = requestHeaders.get("edx-form-id") as string;
  const formData = await request.formData();

  if (!formId) {
    return Response.json({ error: 'Form ID is required' }, { status: 400 });
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
    return Response.json({ error: 'No files provided' }, { status: 400 });
  }

  const result = await uploadUserFilesUseCase({
    formId: formId,
    files: files,
  });

  if (Result.isError(result)) {
    return Response.json({ error: result.message }, { status: 400 });
  }

  return Response.json({ success: true, files: result.value });
}
