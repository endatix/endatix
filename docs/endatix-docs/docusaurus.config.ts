import { themes as prismThemes } from "prism-react-renderer";
import type { Config } from "@docusaurus/types";
import type * as Preset from "@docusaurus/preset-classic";
import type * as Redocusaurus from "redocusaurus";

const config: Config = {
  title: "Endatix Documentation",
  tagline: "Official documentation for Endatix data platform",
  favicon: "img/endatix.svg",

  // Set the production url of your site here
  url: "https://docs.endatix.com",
  // Set the /<baseUrl>/ pathname under which your site is served
  // For GitHub pages deployment, it is often '/<projectName>/'
  baseUrl: "/",

  // GitHub pages deployment config.
  // If you aren't using GitHub pages, you don't need these.
  organizationName: "facebook", // Usually your GitHub org/user name.
  projectName: "docusaurus", // Usually your repo name.

  onBrokenLinks: "throw",
  onBrokenMarkdownLinks: "warn",

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: "en",
    locales: ["en"],
  },

  presets: [
    [
      "classic",
      {
        docs: {
          sidebarPath: "./sidebars.ts",
          // Please change this to your repo.
          // Remove this to remove the "edit this page" links.
          editUrl:
            "https://github.com/endatix/docs-website/issues/new/choose",
        },
        theme: {
          customCss: "./src/css/custom.css",
        },
      } satisfies Preset.Options,
    ],
    [
      "redocusaurus",
      {
        // Plugin Options for loading OpenAPI files
        specs: [
          {
            id: "using-remote-url",
            // Remote File
            spec: "https://app.endatix.com/swagger/Internal%20MVP%20(Alpha)%20Release/swagger.json",
            route: "/docs/api"
          },
        ],
        // Theme Options for modifying how redoc renders them
        theme: {
          primaryColor: "#0054D1"
        },
      },
    ] satisfies Redocusaurus.PresetEntry,
  ],

  themeConfig: {
    // Replace with your project's social card
    image: "img/endatix-transparent.png",
    navbar: {
      title: "Endatix Documentation",
      // logo: {
      //   alt: "Endatix Logo",
      //   src: "img/logo.svg",
      // },
      items: [
        {
          type: "docSidebar",
          sidebarId: "docsSidebar",
          position: "left",
          label: "Docs",
        },
        { to: "/docs/api", label: "API Reference", position: "left" },
        {
          href: "https://github.com/endatix",
          label: "GitHub",
          position: "right",
        },
      ],
    },
    footer: {
      style: "dark",
      links: [
        {
          title: "Docs",
          items: [
            {
              label: "API Reference",
              to: "/docs/api",
            },
          ],
        },
        {
          title: "Community",
          items: [
            {
              label: "Stack Overflow",
              href: "https://stackoverflow.com/questions/tagged/endatix",
            },
            {
              label: "Discord",
              href: "https://discord.gg/VPqzMJgS",
            },
            {
              label: "Twitter",
              href: "https://x.com/endatix_",
            },
          ],
        },
        {
          title: "More",
          items: [
            {
              label: "Web",
              href: "https://endatix.com",
            },
            {
              label: "GitHub",
              href: "https://github.com/endatix",
            },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} Endatix. Built with Docusaurus.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: [ "csharp", "json"]
    },
  } satisfies Preset.ThemeConfig,
  markdown: {
    mermaid: true,
  },
  themes: ['@docusaurus/theme-mermaid'],
};

export default config;
