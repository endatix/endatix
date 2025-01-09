import { describe, it, expect, vi, beforeEach } from "vitest";
import { POST } from "../route";
import * as uploadUserFilesUseCase from "@/features/storage/use-cases/upload-user-files.use-case";
import { Result } from "@/lib/result";

vi.mock("next/headers", () => ({
  headers: () => new Map([["edx-form-id", "test-form-id"]]),
}));

describe("POST /api/public/v0/storage/upload", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("should handle successful file upload", async () => {
    // Arrange
    const mockFileContent = 'test';
    const mockFile = new Blob([mockFileContent], { type: 'image/jpeg' }) as File;
    Object.defineProperty(mockFile, 'name', { value: 'test.jpg' });
    Object.defineProperty(mockFile, 'arrayBuffer', {
      value: async () => new TextEncoder().encode(mockFileContent).buffer
    });
    const formData = new FormData();
    formData.append("test", mockFile);

    const mockResult = Result.success([{ name: "test", url: "test-url" }]);
    vi.spyOn(
      uploadUserFilesUseCase,
      "uploadUserFilesUseCase"
    ).mockResolvedValue(mockResult);

    // Act
    const response = await POST(
      new Request("http://localhost", {
        method: "POST",
        body: formData,
      })
    );
    const data = await response.json();

    // Assert
    expect(response.status).toBe(200);
    expect(data.success).toBe(true);
    expect(data.files).toHaveLength(1);
  });
});
