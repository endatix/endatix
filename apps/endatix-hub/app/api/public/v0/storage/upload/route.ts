import { BlobServiceClient } from '@azure/storage-blob';

async function uploadFile(file: File): Promise<string> {

  const start = performance.now();
  const blobServiceClient = BlobServiceClient.fromConnectionString(process.env.AZURE_STORAGE_CONNECTION_STRING);
  const containerClient = blobServiceClient.getContainerClient(process.env.AZURE_STORAGE_CONTAINER_NAME);
  await containerClient.createIfNotExists({
    access: 'container',
  });

  const folderName = "shesgone";
  const blobClient = containerClient.getBlockBlobClient(`${folderName}/${file.name}`);
  const options = { blobHTTPHeaders: { blobContentType: file.type } };

  const fileBuffer = Buffer.from(await file.arrayBuffer());
  await blobClient.uploadData(fileBuffer, options);

  const end = performance.now();

  console.log(`Upload took ${end - start}ms`);

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
