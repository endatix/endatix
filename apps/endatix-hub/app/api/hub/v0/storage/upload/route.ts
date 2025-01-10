import { getSession } from "@/lib/auth-service";
import { headers } from "next/headers";
import {
  UploadContentFileCommand,
  uploadContentFileUseCase,
} from "@/features/storage/use-cases/upload-content-file.use-case";
import { Result } from "@/lib/result";

export async function POST(request: Request) {
  const session = await getSession();
  if (!session.isLoggedIn) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }

  const requestHeaders = await headers();
  const formId = requestHeaders.get("edx-form-id") as string;

  if (!formId) {
    return Response.json({ error: "Form ID is required" }, { status: 400 });
  }

  const formData = await request.formData();
  const file = formData.get("file");

  if (!file || typeof file === "string") {
    return Response.json({ error: "Invalid or missing file" }, { status: 400 });
  }

  const command: UploadContentFileCommand = {
    formId: formId,
    file: file as File,
  };
  const uploadResult = await uploadContentFileUseCase(command);

  if (Result.isError(uploadResult)) {
    return Response.json({ error: uploadResult.message }, { status: 400 });
  }

  return Response.json(uploadResult.value);
}
