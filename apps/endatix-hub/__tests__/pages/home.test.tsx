import { expect, describe, it } from "vitest";
import { render, screen, within } from "@testing-library/react";
import HomePage from "@/app/(main)/page";

describe("Home Page", () => {
  it("renders the Endatix logo", () => {
    render(<HomePage />);

    const logo = screen.getByAltText("Endatix logo");
    expect(logo).toBeDefined();
    expect(logo.tagName).toBe("IMG");
  });

  it("displays the main description text", () => {
    render(<HomePage />);
    const description = screen.getByText(
      "Endatix Hub is the new exciting way to manage your data collection and processing workflows.",
    );
    expect(description).toBeDefined();
  });

  it("shows the 'Coming soon!' message", () => {
    render(<HomePage />);

    expect(screen.getByText("Coming soon!")).toBeDefined();
  });

  it("renders 'Learn about Endatix' button with correct link", () => {
    render(<HomePage />);

    const learnLink = screen.getByRole("link", { name: "Learn about Endatix" });
    expect(learnLink).toBeDefined();
    expect(learnLink.getAttribute("href")).toBe(
      "https://endatix.com?utm_source=endatix-hub&utm_medium=product",
    );
  });

  it("renders 'Read our Docs' button with correct link", () => {
    render(<HomePage />);

    const docsLink = screen.getByRole("link", { name: "Read our Docs" });
    expect(docsLink).toBeDefined();
    expect(docsLink.getAttribute("href")).toBe(
      "https://docs.endatix.com/docs/category/getting-started?utm_source=endatix-hub&utm_medium=product",
    );
  });

  it("renders all footer links with correct attributes", () => {
    const footerLinks = [
      {
        text: "Learn",
        href: "https://docs.endatix.com?utm_source=endatix-hub&utm_medium=product",
      },
      {
        text: "Follow us on GitHub",
        href: "https://github.com/endatix/endatix?tab=readme-ov-file#endatix-platform",
      },
      {
        text: "Go to endatix.com →",
        href: "https://endatix.com?utm_source=endatix-hub",
      },
    ];

    render(<HomePage />);

    const footer = screen.getByRole("contentinfo");
    footerLinks.forEach(({ text, href }) => {
      const link = within(footer).getByText(text);
      expect(link).toBeDefined();
      expect(link?.getAttribute("href")).toBe(href);
      expect(link?.getAttribute("target")).toBe("_blank");
      expect(link?.getAttribute("rel")).toBe("noopener noreferrer");
    });
  });

  it("ensures GitHub icon is properly hidden from screen readers", () => {
    render(<HomePage />);

    const githubIcon = screen.getByAltText("GitHub icon");
    expect(githubIcon.getAttribute("aria-hidden")).toBe("true");
  });
});
