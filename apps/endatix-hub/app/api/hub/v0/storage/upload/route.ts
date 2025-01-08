import { getSession } from "@/lib/auth-service";
import { StorageService } from "@/lib/storage-service";
import { headers } from "next/headers";

export async function POST(request: Request) {
  const session = await getSession();
  if (!session.isLoggedIn) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }

  const requestHeaders = await headers();
  const formId = requestHeaders.get('edx-form-id') as string;

  if (!formId) {
    return Response.json({ error: 'Form ID is required' }, { status: 400 });
  }

  const formData = await request.formData();
  const file = formData.get('file');

  if (!file || !(file instanceof File)) {
    return Response.json(
      { error: 'Invalid or missing file' },
      { status: 400 }
    );
  }

  try {
    const storageService = new StorageService();
    const fileUrl = await storageService.uploadFormContentFile(file, formId);
    return Response.json({ 
      success: true,
      name: file.name,
      url: fileUrl
    });
  } catch {
    return Response.json(
      { error: 'Upload to storage failed' },
      { status: 400 }
    );
  }
}