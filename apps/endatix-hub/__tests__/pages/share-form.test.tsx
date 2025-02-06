import ShareFormPage from "@/app/(public)/share/[formId]/page";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { Result } from "@/lib/result";
import { getActiveDefinitionUseCase } from "@/features/public-form/use-cases/get-active-definition.use-case";
import { getPartialSubmissionUseCase } from "@/features/public-form/use-cases/get-partial-submission.use-case";

vi.mock(
  "@/features/public-form/use-cases/get-active-definition.use-case",
  () => ({
    getActiveDefinitionUseCase: vi.fn(),
  }),
);
vi.mock(
  "@/features/public-form/use-cases/get-partial-submission.use-case",
  () => ({
    getPartialSubmissionUseCase: vi.fn(),
  }),
);
vi.mock("next/headers", () => ({
  cookies: vi.fn().mockResolvedValue({}),
}));
vi.mock("@/features/public-form/infrastructure/cookie-store", () => ({
  FormTokenCookieStore: vi.fn().mockResolvedValue({}),
}));

describe("ShareForm Page", async () => {
  beforeEach(() => {
    vi.resetModules();
    vi.clearAllMocks();
  });

  it("displays form not found message when definition is not found", async () => {
    vi.mocked(getActiveDefinitionUseCase).mockResolvedValue(
      Result.error("Form not found"),
    );
    vi.mocked(getPartialSubmissionUseCase).mockResolvedValue(
      Result.error("Submission not found"),
    );

    const props = {
      params: Promise.resolve({ formId: "invalid-id" }),
    };
    const component = await ShareFormPage(props);
    render(component);

    const errorMessage = await screen.findByText("Form not found");
    expect(errorMessage).toBeDefined();
  });
});
