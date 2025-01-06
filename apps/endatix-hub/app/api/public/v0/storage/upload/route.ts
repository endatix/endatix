import { BlobServiceClient } from '@azure/storage-blob'
import { optimizeImage } from 'next/dist/server/image-optimizer'
import { v4 as uuidv4 } from 'uuid'

async function uploadFile(file: File, folderPath: string): Promise<string> {
  if (!file) {
    throw new Error('a file is not provided');
  }

  const fileBuffer = Buffer.from(await file.arrayBuffer());

  let optimizedImageBuffer: Buffer | undefined;

  const STEP_IMAGE_RESIZE_START = performance.now();
  if (process.env.RESIZE_IMAGES && file.type.startsWith('image/')) {
    let width = 800;
    if (process.env.RESIZE_IMAGES_WIDTH) {
      const parsedWidth = Number.parseInt(process.env.RESIZE_IMAGES_WIDTH);
      if (!isNaN(parsedWidth)) {
        width = parsedWidth;
      }
    }

    optimizedImageBuffer = await optimizeImage({
      buffer: fileBuffer,
      contentType: file.type,
      quality: 80,
      width: width,
    });
  }
  const STEP_IMAGE_RESIZE_END = performance.now();
  console.log(
    `⏱️ Image resize took ${STEP_IMAGE_RESIZE_END - STEP_IMAGE_RESIZE_START}ms`
  );

  const STEP_UPLOAD_START = performance.now();
  if (
    !process.env.AZURE_STORAGE_CONNECTION_STRING ||
    !process.env.AZURE_STORAGE_CONTAINER_NAME
  ) {
    throw new Error('BLOB storage connection string or container name not set');
  }

  const blobServiceClient = BlobServiceClient.fromConnectionString(
    process.env.AZURE_STORAGE_CONNECTION_STRING
  );
  const containerClient = blobServiceClient.getContainerClient(
    process.env.AZURE_STORAGE_CONTAINER_NAME
  );

  await containerClient.createIfNotExists({
    access: 'container',
  });

  const uuid = uuidv4();
  const fileExtension = file.name.split('.').pop() || '';
  const blobName = `${folderPath}/${uuid}.${fileExtension}`;
  const blobClient = containerClient.getBlockBlobClient(blobName);
  const options = {
    blobHTTPHeaders: {
      blobContentType: file.type,
    },
  };

  await blobClient.uploadData(optimizedImageBuffer ?? fileBuffer, options);

  const STEP_UPLOAD_END = performance.now();
  console.log(
    `⏱️ Upload to blob took ${STEP_UPLOAD_END - STEP_UPLOAD_START}ms`
  );

  return blobClient.url;
}

export async function POST(request: Request) {
  const formData = await request.formData();
  const formId = formData.get('formId') as string;

  if (!formId) {
    return Response.json({ error: 'Form ID is required' }, { status: 400 });
  }

  const files: { name: string; url: string }[] = [];
  const folderPath = `${formId}`;

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
      const fileUrl = await uploadFile(file, folderPath);
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