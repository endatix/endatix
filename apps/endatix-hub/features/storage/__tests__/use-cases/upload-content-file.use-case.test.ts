import { describe, it, expect, vi, beforeEach } from "vitest";

import { StorageService } from "@/features/storage/infrastructure/storage-service";
import { Result } from "@/lib/result";
import { uploadContentFileUseCase } from "@/features/storage/use-cases/upload-content-file.use-case";

vi.mock("@/features/storage/infrastructure/storage-service");

describe("uploadContentFileUseCase", () => {
  const mockFileContent = "test";
  const createMockFile = (name: string) => {
    const file = new Blob([mockFileContent], { type: "image/jpeg" }) as File;
    Object.defineProperty(file, "name", { value: name });
    Object.defineProperty(file, "arrayBuffer", {
      value: async () => new TextEncoder().encode(mockFileContent).buffer,
    });
    return file;
  };
  const mockFile = createMockFile("test.jpg");
  const mockCommand = {
    formId: "form-123",
    file: mockFile,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("should return validation error when formId is empty", async () => {
    // Act
    const result = await uploadContentFileUseCase({
      formId: "",
      file: mockFile,
    });

    // Assert
    expect(Result.isError(result)).toBe(true);
    expect((result as unknown as Error).message).toBe("Form ID is required");
  });

  it("should return validation error when file is missing", async () => {
    // Act
    const result = await uploadContentFileUseCase({
      formId: "form-123",
      file: undefined as unknown as File,
    });

    // Assert
    expect(Result.isError(result)).toBe(true);
    expect((result as unknown as Error).message).toBe("File is required");
  });

  it("should successfully upload file and return url", async () => {
    // Arrange
    const mockUrl = "https://storage.test/test.jpg";
    vi.spyOn(StorageService.prototype, "uploadToStorage").mockResolvedValue(
      mockUrl
    );
    vi.spyOn(StorageService.prototype, "optimizeImageSize").mockResolvedValue(
      Buffer.from("optimized")
    );

    // Act
    const result = await uploadContentFileUseCase(mockCommand);

    // Assert
    expect(Result.isSuccess(result)).toBe(true);
    if (Result.isSuccess(result)) {
      expect(result.value).toEqual({ name: mockFile.name, url: mockUrl });
    }
  });

  it("should optimize image before upload for image files", async () => {
    // Arrange
    const optimizeSpy = vi
      .spyOn(StorageService.prototype, "optimizeImageSize")
      .mockResolvedValue(Buffer.from("optimized"));
    vi.spyOn(StorageService.prototype, "uploadToStorage").mockResolvedValue(
      "mock-url"
    );

    // Act
    await uploadContentFileUseCase(mockCommand);

    // Assert
    expect(optimizeSpy).toHaveBeenCalled();
  });

  it("should return error when fileExtension is not supported", async () => {
    // Arrange
    const mockFileWithoutExtension = createMockFile("noextension_name.");
    const mockCommandWithoutExtension = {
      formId: "form-123",
      file: mockFileWithoutExtension,
    };

    // Act
    const result = await uploadContentFileUseCase(mockCommandWithoutExtension);

    // Assert
    expect(Result.isError(result)).toBe(true);
    expect((result as unknown as Error).message).toBe("File extension is required. Please provide a valid file.");
  });
});
