import { BlobServiceClient, StorageSharedKeyCredential } from "@azure/storage-blob";
import { optimizeImage } from "next/dist/server/image-optimizer";

const DEFAULT_IMAGE_WIDTH = 800;

type AzureStorageConfig = {
  isEnabled: boolean;
  accountName: string;
  accountKey: string;
  hostName: string;
}

export class StorageService {
  private readonly blobServiceClient: BlobServiceClient;

  constructor() {
    const config = StorageService.getAzureStorageConfig();
    if (!config.isEnabled) {
      throw new Error("Azure storage is not enabled");
    }

    this.blobServiceClient = new BlobServiceClient(
      `https://${config.hostName}`,
      new StorageSharedKeyCredential(config.accountName, config.accountKey)
    );
  }

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
    fileName: string,
    containerName: string,
    folderPath?: string,
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

    const containerClient = this.blobServiceClient.getContainerClient(containerName);
    await containerClient.createIfNotExists({
      access: "container",
    });

    const blobName = folderPath ? `${folderPath}/${fileName}` : fileName;
    const blobClient = containerClient.getBlockBlobClient(blobName);
    await blobClient.uploadData(fileBuffer);

    const STEP_UPLOAD_END = performance.now();
    console.log(
      `⏱️ Upload to blob took ${STEP_UPLOAD_END - STEP_UPLOAD_START}ms`
    );

    return blobClient.url;
  }

  static getAzureStorageConfig(): AzureStorageConfig {
    const { AZURE_STORAGE_ACCOUNT_NAME, AZURE_STORAGE_ACCOUNT_KEY } = process.env;
    const isEnabled = !!AZURE_STORAGE_ACCOUNT_NAME && !!AZURE_STORAGE_ACCOUNT_KEY;
    if (!isEnabled) {
      return { 
        isEnabled: false,
        accountName: "",
        accountKey: "",
        hostName: "",
      };
    }

    return {
      isEnabled: true,
      accountName: AZURE_STORAGE_ACCOUNT_NAME,
      accountKey: AZURE_STORAGE_ACCOUNT_KEY,
      hostName: `${AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net`,
    };
  }
}
