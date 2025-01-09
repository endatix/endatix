import { describe, it, expect, vi, beforeEach } from "vitest";
import { POST } from "../route";
import * as authService from "@/lib/auth-service";
import * as uploadContentFileUseCase from "@/features/storage/use-cases/upload-content-file.use-case";
import { Result } from "@/lib/result";
import { SessionData } from "@/lib/auth-service";

vi.mock("@/lib/auth-service");
vi.mock("next/headers", () => ({
  headers: () => new Map([["edx-form-id", "test-form-id"]]),
}));

describe("POST /api/hub/v0/storage/upload", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.spyOn(authService, "getSession").mockResolvedValue({
      isLoggedIn: true,
      username: "test",
      accessToken: "test",
      refreshToken: "test",
    } as SessionData);
  });

  it("should handle unauthorized access", async () => {
    // Arrange
    vi.spyOn(authService, "getSession").mockResolvedValue({
      isLoggedIn: false,
      username: "test",
      accessToken: "test",
      refreshToken: "test",
    } as SessionData);

    // Act
    const response = await POST(new Request("http://localhost"));
    const data = await response.json();

    // Assert
    expect(response.status).toBe(401);
    expect(data.error).toBe("Unauthorized");
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
    formData.append('file', mockFile);

    const mockResult = Result.success({ 
      name: "test", 
      url: "test-url" 
    });

    vi.spyOn(
      uploadContentFileUseCase,
      "uploadContentFileUseCase"
    ).mockResolvedValue(mockResult);

    // Act
    const response = await POST(
      new Request("http://localhost", {
        method: "POST",
        body: formData
      })
    );

    const data = await response.json();

    // Assert
    expect(response.status).toBe(200);
    expect(data.url).toBe("test-url");
    expect(uploadContentFileUseCase.uploadContentFileUseCase).toHaveBeenCalledWith({
      formId: "test-form-id",
      file: expect.any(Object) as File,
    });
  }, 10000); // Increased timeout for this test
});
