import { expect, describe, it, afterEach, beforeEach } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import Home from "@/app/(main)/page";

describe("Homepage", () => {
    beforeEach(() => {
        render(<Home />);
    });

    afterEach(() => {
        cleanup();
    });

    it("renders the Endatix logo", () => {
        const logo = screen.getByAltText("Endatix logo");
        expect(logo).toBeDefined();
        expect(logo.tagName).toBe("IMG");
    });

    it("displays the main description text", () => {
        const description = screen.getByText(
            "Endatix Hub is the new exciting way to manage your data collection and processing workflows."
        );
        expect(description).toBeDefined();
    });

    it("shows the 'Coming soon!' message", () => {
        expect(screen.getByText("Coming soon!")).toBeDefined();
    });

    it("renders 'Learn about Endatix' button with correct link", () => {
        const learnLink = screen.getByRole("link", { name: "Learn about Endatix" });
        expect(learnLink).toBeDefined();
        expect(learnLink.getAttribute("href")).toBe(
            "https://endatix.com?utm_source=endatix-hub&utm_medium=product"
        );
    });

    it("renders 'Read our Docs' button with correct link", () => {
        const docsLink = screen.getByRole("link", { name: "Read our Docs" });
        expect(docsLink).toBeDefined();
        expect(docsLink.getAttribute("href")).toBe(
            "https://docs.endatix.com/docs/category/getting-started?utm_source=endatix-hub&utm_medium=product"
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

        const footer = screen.getByRole("contentinfo");
        footerLinks.forEach(({ text, href }) => {
            const link = footer.querySelector(`a[href^='${href}']`);
            expect(link).toBeDefined();
            expect(link?.getAttribute("href")).toBe(href);
            expect(link?.getAttribute("target")).toBe("_blank");
            expect(link?.getAttribute("rel")).toBe("noopener noreferrer");
        });
    });

    it("ensures GitHub icon is properly hidden from screen readers", () => {
        const githubIcon = screen.getByAltText("GitHub icon");
        expect(githubIcon.getAttribute("aria-hidden")).toBe("true");
    });
});