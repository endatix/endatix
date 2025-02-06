import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ChangePasswordForm } from "../change-password-form";
import { changePasswordAction } from "@/features/my-account/application/actions/change-password.action";

// Mock the server action
vi.mock(
  "@/features/my-account/application/actions/change-password.action",
  () => ({
    changePasswordAction: vi.fn(),
  }),
);

describe("ChangePasswordForm", () => {
  it("renders all password input fields", () => {
    render(<ChangePasswordForm />);

    expect(screen.getByLabelText(/current password/i)).toBeDefined();
    expect(screen.getByLabelText(/new password/i)).toBeDefined();
    expect(screen.getByLabelText(/confirm password/i)).toBeDefined();
    expect(
      screen.getByRole("button", { name: /change password/i }),
    ).toBeDefined();
  });

  it("displays validation errors for empty fields", async () => {
    vi.mocked(changePasswordAction).mockResolvedValueOnce({
      success: false,
      errors: {
        currentPassword: ["Current password is required"],
        newPassword: ["New password is required"],
        confirmPassword: ["Confirm password is required"],
      },
      errorMessage: "Could not change password",
    });

    render(<ChangePasswordForm />);

    const submitButton = screen.getByRole("button", {
      name: /change password/i,
    });
    fireEvent.click(submitButton);

    expect(
      await screen.findByText(/current password is required/i),
    ).toBeDefined();
    expect(await screen.findByText(/new password is required/i)).toBeDefined();
    expect(
      await screen.findByText(/confirm password is required/i),
    ).toBeDefined();
  });

  it("displays success message on successful password change", async () => {
    vi.mocked(changePasswordAction).mockResolvedValueOnce({
      success: true,
      errors: {},
      errorMessage: "",
    });

    render(<ChangePasswordForm />);

    const currentPasswordInput = screen.getByLabelText(/current password/i);
    const newPasswordInput = screen.getByLabelText(/new password/i);
    const confirmPasswordInput = screen.getByLabelText(/confirm password/i);
    const submitButton = screen.getByRole("button", {
      name: /change password/i,
    });

    fireEvent.change(currentPasswordInput, {
      target: { value: "CurrentPass123" },
    });
    fireEvent.change(newPasswordInput, { target: { value: "NewPass123" } });
    fireEvent.change(confirmPasswordInput, { target: { value: "NewPass123" } });
    fireEvent.click(submitButton);

    expect(
      await screen.findByText(/Your password has been changed successfully/i),
    ).toBeDefined();
  });

  it("displays error message on API failure", async () => {
    vi.mocked(changePasswordAction).mockResolvedValueOnce({
      success: false,
      errors: {},
      errorMessage: "Failed to change password",
    });

    render(<ChangePasswordForm />);

    const currentPasswordInput = screen.getByLabelText(/current password/i);
    const newPasswordInput = screen.getByLabelText(/new password/i);
    const confirmPasswordInput = screen.getByLabelText(/confirm password/i);
    const submitButton = screen.getByRole("button", {
      name: /change password/i,
    });

    fireEvent.change(currentPasswordInput, {
      target: { value: "CurrentPass123" },
    });
    fireEvent.change(newPasswordInput, { target: { value: "NewPass123" } });
    fireEvent.change(confirmPasswordInput, { target: { value: "NewPass123" } });
    fireEvent.click(submitButton);

    expect(await screen.findByText(/failed to change password/i)).toBeDefined();
  });
});
