import { BlobServiceClient } from '@azure/storage-blob';
import { optimizeImage } from 'next/dist/server/image-optimizer';

async function uploadFile(file: File): Promise<string> {
  const blobServiceClient = BlobServiceClient.fromConnectionString(process.env.AZURE_STORAGE_CONNECTION_STRING);
  const containerClient = blobServiceClient.getContainerClient(process.env.AZURE_STORAGE_CONTAINER_NAME);
  await containerClient.createIfNotExists({
    access: 'container',
  });

  const folderName = "shesgone";
  const blobClient = containerClient.getBlockBlobClient(`${folderName}/${file.name}`);
  const options = { blobHTTPHeaders: { blobContentType: file.type } };

  const fileBuffer = Buffer.from(await file.arrayBuffer());

  const STEP_IMAGE_RESIZE_START = performance.now();

  const optimizedImageBuffer = await optimizeImage({
    buffer: fileBuffer,
    contentType: file.type,
    quality: 80,
    width: 800,
  });

  const STEP_IMAGE_RESIZE_END = performance.now();

  console.log(`⏱️ Image resize took ${STEP_IMAGE_RESIZE_END - STEP_IMAGE_RESIZE_START}ms`);

  const STEP_UPLOAD_START = performance.now();

  await blobClient.uploadData(optimizedImageBuffer, options);

  const STEP_UPLOAD_END = performance.now();

  console.log(`⏱️ Upload to blob took ${STEP_UPLOAD_END - STEP_UPLOAD_START}ms`);

  return blobClient.url;
}

export async function POST(request: Request) {
  try {
    const formData = await request.formData();
    const files: { name: string; url: string }[] = [];

    for (const [filename, file] of formData.entries()) {
      if (!(file instanceof File)) {
        return Response.json({ error: `Invalid file for ${filename}` }, { status: 400 });
      }

      const fileUrl = await uploadFile(file as File);
      files.push({name: filename, url: fileUrl});
    }

    if (files.length === 0) {
      return Response.json({ error: 'No files provided' }, { status: 400 });
    }

    return Response.json({ success: true, files });
  } catch (error) {
    return Response.json({ error: 'Invalid request' }, { status: 400 });
  }
}
