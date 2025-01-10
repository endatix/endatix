import { describe, it, expect, vi, beforeEach } from "vitest";

import { StorageService } from "@/features/storage/infrastructure/storage-service";
import { Result } from "@/lib/result";
import { uploadContentFileUseCase } from "@/features/storage/use-cases/upload-content-file.use-case";

vi.mock("@/features/storage/infrastructure/storage-service");

describe("uploadContentFileUseCase", () => {
  const mockFileContent = "test";
  const mockFile = new Blob([mockFileContent], { type: "image/jpeg" }) as File;
  Object.defineProperty(mockFile, "name", { value: "test.jpg" });
  Object.defineProperty(mockFile, "arrayBuffer", {
    value: async () => new TextEncoder().encode(mockFileContent).buffer,
  });
  const mockCommand = {
    formId: "form-123",
    file: mockFile,
  };

  beforeEach(() => {
    vi.clearAllMocks();
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
});
