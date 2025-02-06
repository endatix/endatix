import { describe, it, expect, vi, beforeEach } from "vitest";
import { changePassword } from "../api";

// Mock fetch
const mockFetch = vi.fn();
global.fetch = mockFetch;

// Mock getSession
vi.mock("@/lib/auth-service", () => ({
  getSession: vi.fn().mockResolvedValue({
    accessToken: "mock-access-token",
    refreshToken: "mock-refresh-token",
    username: "test-user",
    isLoggedIn: true,
  }),
}));

describe("API Service - Change Password", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    process.env.ENDATIX_BASE_URL = "https://mock.endatix.com";
  });

  it("should call the change password endpoint with correct data", async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve({}),
    });

    const currentPassword = "currentPass123";
    const newPassword = "newPass123";
    const confirmPassword = "newPass123";

    await changePassword(currentPassword, newPassword, confirmPassword);

    expect(mockFetch).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({
          currentPassword,
          newPassword,
          confirmPassword,
        }),
      }),
    );
  });

  it("should throw an error when the API call fails", async () => {
    const errorMessage = "Failed to change password";
    mockFetch.mockResolvedValueOnce({
      ok: false,
      json: () => Promise.resolve({ message: errorMessage }),
    });

    const currentPassword = "currentPass123";
    const newPassword = "newPass123";
    const confirmPassword = "newPass123";

    await expect(
      changePassword(currentPassword, newPassword, confirmPassword),
    ).rejects.toThrow(errorMessage);
  });

  it("should throw an error when the API response is not ok", async () => {
    mockFetch.mockRejectedValueOnce(new Error("Network error"));

    const currentPassword = "currentPass123";
    const newPassword = "newPass123";
    const confirmPassword = "newPass123";

    await expect(
      changePassword(currentPassword, newPassword, confirmPassword),
    ).rejects.toThrow("Network error");
  });
});
