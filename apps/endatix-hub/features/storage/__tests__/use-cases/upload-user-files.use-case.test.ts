import { describe, it, expect, vi, beforeEach } from "vitest";
import { uploadUserFilesUseCase } from "@/features/storage/use-cases/upload-user-files.use-case";
import { StorageService } from "@/features/storage/infrastructure/storage-service";
import { Result } from "@/lib/result";

vi.mock("@/features/storage/infrastructure/storage-service");

describe("uploadUserFilesUseCase", () => {
  const mockFileContent = "test";
  const mockFile = new Blob([mockFileContent], { type: "image/jpeg" }) as File;
  Object.defineProperty(mockFile, "name", { value: "test.jpg" });
  Object.defineProperty(mockFile, "arrayBuffer", {
    value: async () => new TextEncoder().encode(mockFileContent).buffer,
  });

  const mockCommand = {
    formId: "form-123",
    submissionId: "sub-123",
    files: [{ name: "test", file: mockFile }],
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("should successfully upload files and return urls", async () => {
    // Arrange
    const mockUrl = "https://storage.test/test.jpg";
    vi.spyOn(StorageService.prototype, "uploadToStorage").mockResolvedValue(
      mockUrl,
    );
    vi.spyOn(StorageService.prototype, "optimizeImageSize").mockResolvedValue(
      Buffer.from("optimized"),
    );

    // Act
    const result = await uploadUserFilesUseCase(mockCommand);

    // Assert
    expect(Result.isSuccess(result)).toBe(true);
    if (Result.isSuccess(result)) {
      expect(result.value).toEqual([{ name: "test", url: mockUrl }]);
    }
  });

  it("should return validation error when formId is missing", async () => {
    const result = await uploadUserFilesUseCase({
      ...mockCommand,
      formId: "",
    });

    expect(Result.isError(result)).toBe(true);
    if (Result.isError(result)) {
      expect(result.message).toBe("Form ID is required");
    }
  });
});
