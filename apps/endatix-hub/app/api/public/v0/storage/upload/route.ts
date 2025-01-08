import { StorageService } from "@/lib/storage-service";

export async function POST(request: Request) {
  const formData = await request.formData();
  const formId = formData.get('formId') as string;

  if (!formId) {
    return Response.json({ error: 'Form ID is required' }, { status: 400 });
  }

  const files: { name: string; url: string }[] = [];

  for (const [filename, file] of formData.entries()) {
    if (filename === 'formId') {
      continue;
    }

    if (!file || !(file instanceof File)) {
      return Response.json(
        { error: `Invalid file for ${filename}` },
        { status: 400 }
      );
    }

    try {
      const storageService = new StorageService();
      const fileUrl = await storageService.uploadUserFile(file, formId);
      files.push({ name: filename, url: fileUrl });
    } catch {
      return Response.json(
        { error: 'Upload to storage failed' },
        { status: 400 }
      );
    }
  }

  if (files.length === 0) {
    return Response.json({ error: 'No files provided' }, { status: 400 });
  }

  return Response.json({ success: true, files });
}