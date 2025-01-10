import { BlobServiceClient } from "@azure/storage-blob";
import { optimizeImage } from "next/dist/server/image-optimizer";

const DEFAULT_IMAGE_WIDTH = 800;

export class StorageService {
  async optimizeImageSize(
    imageBuffer: Buffer,
    contentType: string,
    quality: number = 80
  ): Promise<Buffer> {
    if (!contentType) {
      throw new Error("contentType is not provided");
    }

    if (!imageBuffer) {
      throw new Error("imageBuffer is not provided");
    }

    const resizeImagesRawValue =  process.env.RESIZE_IMAGES?.toLowerCase();
    const resizeImagesParsedValue = resizeImagesRawValue === 'true' || resizeImagesRawValue === '1';
    const shouldResize = resizeImagesParsedValue === true && contentType.startsWith("image/");
    if (!shouldResize) {
      return imageBuffer;
    }

    const STEP_IMAGE_RESIZE_START = performance.now();

    let width = DEFAULT_IMAGE_WIDTH;
    if (process.env.RESIZE_IMAGES_WIDTH) {
      const parsedWidth = Number.parseInt(process.env.RESIZE_IMAGES_WIDTH);
      if (!isNaN(parsedWidth)) {
        width = parsedWidth;
      }
    }

    const optimizedImageBuffer = await optimizeImage({
      buffer: imageBuffer,
      contentType: contentType,
      quality: quality,
      width: width,
    });

    const STEP_IMAGE_RESIZE_END = performance.now();
    console.log(
      `⏱️ Image resize took ${
        STEP_IMAGE_RESIZE_END - STEP_IMAGE_RESIZE_START
      }ms`
    );

    return optimizedImageBuffer;
  }

  async uploadToStorage(
    fileBuffer: Buffer,
    folderPath: string,
    fileName: string,
    containerName: string
  ): Promise<string> {
    if (!fileBuffer) {
      throw new Error("a file is not provided");
    }

    if (!fileName) {
      throw new Error("fileName is not provided");
    }

    if (!containerName) {
      throw new Error("container name is not provided");
    }

    const STEP_UPLOAD_START = performance.now();
    if (!process.env.AZURE_STORAGE_CONNECTION_STRING) {
      throw new Error("BLOB storage connection string not set");
    }

    const blobServiceClient = BlobServiceClient.fromConnectionString(
      process.env.AZURE_STORAGE_CONNECTION_STRING
    );
    const containerClient = blobServiceClient.getContainerClient(containerName);
    await containerClient.createIfNotExists({
      access: "container",
    });

    const blobName = `${folderPath}/${fileName}`;
    const blobClient = containerClient.getBlockBlobClient(blobName);
    await blobClient.uploadData(fileBuffer);

    const STEP_UPLOAD_END = performance.now();
    console.log(
      `⏱️ Upload to blob took ${STEP_UPLOAD_END - STEP_UPLOAD_START}ms`
    );

    return blobClient.url;
  }
}
