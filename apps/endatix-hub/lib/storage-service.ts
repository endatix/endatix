import { BlobServiceClient } from "@azure/storage-blob";
import { optimizeImage } from "next/dist/server/image-optimizer";
import { v4 as uuidv4 } from "uuid";

const DEFAULT_USER_FILES_CONTAINER_NAME = "user-files";
const DEFAULT_FORM_CONTENT_FILES_CONTAINER_NAME = "content";

export class StorageService {
  async uploadUserFile(file: File, formId: string): Promise<string> {
    if (!formId) {
      throw new Error("formId is not provided");
    }

    const folderPath = `${formId}`;
    const containerName =
      process.env.USER_FILES_STORAGE_CONTAINER_NAME ??
      DEFAULT_USER_FILES_CONTAINER_NAME;

    return await this.uploadFileToStorage(file, folderPath, containerName);
  }

  async uploadFormContentFile(file: File, formId: string): Promise<string> {
    if (!formId) {
      throw new Error("formId is not provided");
    }

    const folderPath = `assets/${formId}`;
    const containerName =
      process.env.CONTENT_STORAGE_CONTAINER_NAME ??
      DEFAULT_FORM_CONTENT_FILES_CONTAINER_NAME;

    return await this.uploadFileToStorage(file, folderPath, containerName);
  }

  private async uploadFileToStorage(
    file: File,
    folderPath: string,
    containerName: string
  ): Promise<string> {
    if (!file) {
      throw new Error("a file is not provided");
    }

    if (!containerName) {
      throw new Error("container name is not provided");
    }

    const fileBuffer = Buffer.from(await file.arrayBuffer());

    let optimizedImageBuffer: Buffer | undefined;

    if (process.env.RESIZE_IMAGES && file.type.startsWith("image/")) {
      const STEP_IMAGE_RESIZE_START = performance.now();

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

      const STEP_IMAGE_RESIZE_END = performance.now();
      console.log(
        `⏱️ Image resize took ${
          STEP_IMAGE_RESIZE_END - STEP_IMAGE_RESIZE_START
        }ms`
      );
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

    const uuid = uuidv4();
    const fileExtension = file.name.split(".").pop() || "";
    const blobName = `${folderPath}/${uuid}.${fileExtension}`;
    const blobClient = containerClient.getBlockBlobClient(blobName);
    await blobClient.uploadData(optimizedImageBuffer ?? fileBuffer);

    const STEP_UPLOAD_END = performance.now();
    console.log(
      `⏱️ Upload to blob took ${STEP_UPLOAD_END - STEP_UPLOAD_START}ms`
    );

    return blobClient.url;
  }
}
