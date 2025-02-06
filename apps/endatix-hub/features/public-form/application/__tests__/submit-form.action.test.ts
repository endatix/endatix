import { describe, it, expect, vi, Mock, beforeEach } from "vitest";
import { cookies } from "next/headers";
import { ErrorType, Result } from "@/lib/result";
import { createSubmissionPublic, updateSubmissionPublic } from "@/services/api";
import { submitFormAction } from "@/features/public-form/application/actions/submit-form.action";

const COOKIE_NAME = "FPSK";
vi.mock("next/headers", () => ({
  cookies: vi.fn(() => ({
    get: vi.fn(),
    set: vi.fn(),
    delete: vi.fn(),
  })),
}));

vi.mock("@/services/api", () => ({
  createSubmissionPublic: vi.fn(),
  updateSubmissionPublic: vi.fn(),
}));

describe("submitFormAction", () => {
  const mockCookieStore = {
    get: vi.fn(),
    set: vi.fn(),
    delete: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    process.env.NEXT_FORMS_COOKIE_NAME = COOKIE_NAME;
    process.env.NEXT_FORMS_COOKIE_DURATION_DAYS = "7";
    (cookies as Mock).mockReturnValue(mockCookieStore);
  });

  it("should create new submission and update cookie when no token exists", async () => {
    // Arrange
    mockCookieStore.get.mockReturnValue(undefined);
    const mockSubmissionData = {
      jsonData: '{"test": true}',
      isComplete: false,
      currentPage: 1,
    };

    const mockCreateResponse = {
      token: "new-token",
      isComplete: false,
    };
    (createSubmissionPublic as Mock).mockResolvedValue(mockCreateResponse);

    // Act
    const result = await submitFormAction("form-1", mockSubmissionData);

    // Assert
    expect(createSubmissionPublic).toHaveBeenCalledWith(
      "form-1",
      mockSubmissionData,
    );
    expect(mockCookieStore.set).toHaveBeenCalledWith(
      COOKIE_NAME,
      expect.stringContaining("new-token"),
      expect.any(Object),
    );
    expect(Result.isSuccess(result)).toBe(true);
  });

  it("should update existing submission and delete cookie value when token exists", async () => {
    // Arrange
    mockCookieStore.get.mockReturnValue({
      value: JSON.stringify({ "form-1": "existing-token" }),
    });

    const mockSubmissionData = {
      jsonData: '{"test": true}',
      isComplete: true,
      currentPage: 2,
    };

    const mockUpdateResponse = {
      isComplete: true,
    };
    (updateSubmissionPublic as Mock).mockResolvedValue(mockUpdateResponse);

    // Act
    const result = await submitFormAction("form-1", mockSubmissionData);

    // Assert
    expect(updateSubmissionPublic).toHaveBeenCalledWith(
      "form-1",
      "existing-token",
      mockSubmissionData,
    );
    expect(mockCookieStore.delete).toHaveBeenCalledWith(COOKIE_NAME);
    expect(Result.isSuccess(result)).toBe(true);
  });

  it("should return a Result.error when the submission API call fails", async () => {
    // Arrange
    mockCookieStore.get.mockReturnValue(undefined);
    (createSubmissionPublic as Mock).mockRejectedValue(new Error("API Error"));

    // Act
    const result = await submitFormAction("form-1", {
      jsonData: '{"test": true}',
      isComplete: false,
    });

    // Assert
    expect(Result.isError(result)).toBe(true);
    expect(result.kind).toBe(ErrorType.Error);
  });
});
