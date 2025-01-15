import { Result } from "@/lib/result";
import { v4 as uuidv4 } from "uuid";
import { StorageService } from "../infrastructure/storage-service";

export type UploadContentFileCommand = {
  formId: string;
  file: File;
};

export type UploadFileResult = {
  name: string;
  url: string;
};

export type UploadContentFileResult = Result<UploadFileResult>;

const DEFAULT_FORM_CONTENT_FILES_CONTAINER_NAME = "content";

export const uploadContentFileUseCase = async ({
  formId,
  file,
}: UploadContentFileCommand): Promise<UploadContentFileResult> => {
  if (!formId) {
    return Result.validationError("Form ID is required");
  }

  if (!file) {
    return Result.validationError("File is required");
  }

  const folderPath = `f/${formId}`;
  const containerName =
    process.env.CONTENT_STORAGE_CONTAINER_NAME ??
    DEFAULT_FORM_CONTENT_FILES_CONTAINER_NAME;

  try {
    const storageService = new StorageService();
    let fileBuffer = Buffer.from(await file.arrayBuffer());

    if (file.type.startsWith("image/")) {
      fileBuffer = await storageService.optimizeImageSize(fileBuffer, file.type);
    }

    const uuid = uuidv4();
    const fileExtension = file.name.split(".").pop();

    if (!fileExtension) {
      return Result.validationError(
        "File extension is required. Please provide a valid file."
      );
    }

    const fileName = `${uuid}.${fileExtension}`;
    const fileUrl = await storageService.uploadToStorage(
      fileBuffer,
      fileName,
      containerName,
      folderPath
    );

    return Result.success({
      name: file.name,
      url: fileUrl,
    });
  } catch (err) {
    return Result.error(
      "Failed to upload file",
      err instanceof Error ? err.message : "Unknown error"
    );
  }
};
