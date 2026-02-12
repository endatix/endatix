import { themes as prismThemes } from "prism-react-renderer";
import type { Config } from "@docusaurus/types";
import type * as Preset from "@docusaurus/preset-classic";
import type * as Redocusaurus from "redocusaurus";

const config: Config = {
  title: "Endatix Documentation",
  tagline:
    "Self-Hosted Alternative to SaaS Form or Survey Platforms",
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

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: "en",
    locales: ["en"],
  },
  future: {
    v4: true,
  },

  presets: [
    [
      "classic",
      {
        blog: false,
        docs: {
          sidebarPath: "./sidebars.ts",
          // Please change this to your repo.
          // Remove this to remove the "edit this page" links.
          editUrl:
            "https://github.com/endatix/endatix/tree/main/docs/endatix-docs",
        },
        theme: {
          customCss: "./src/css/endatix-theme.css",
        },
        gtag: {
          trackingID: "G-EX59EFQH18",
          anonymizeIP: true,
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
            spec: "./swagger.json",
            route: "/docs/developers/api/api-reference",
          },
        ],
        // Theme Options for modifying how redoc renders them
        theme: {
          primaryColor: "#0066FF",
        },
      },
    ] satisfies Redocusaurus.PresetEntry,
  ],

  themeConfig: {
    // Replace with your project's social card
    image: "img/endatix-transparent.png",
    navbar: {
      logo: {
        alt: "Endatix Logo",
        src: "img/endatix.svg",
      },
      items: [
        {
          type: "docSidebar",
          sidebarId: "devSidebar",
          position: "left",
          label: "Developers",
        },
        {
          type: "docSidebar",
          sidebarId: "userSidebar",
          position: "left",
          label: "End Users",
        },
        {
          href: "https://github.com/endatix",
          label: "GitHub",
          position: "right",
        },
      ],
    },
    footer: {
      style: "light",
      links: [
        {
          title: "Endatix.com",
          items: [
            {
              label: "Blog",
              href: "https://endatix.com/blog",
            },
            {
              label: "Product",
              href: "https://endatix.com/products",
            },
            {
              label: "Services",
              href: "https://endatix.com/services",
            },
            {
              label: "Contact us",
              href: "https://endatix.com/contact",
            },
          ],
        },
        {
          title: "Resources",
          items: [
            {
              label: "Support",
              href: "https://github.com/endatix/endatix/issues/new/choose",
            },
            {
              label: "Releases",
              href: "https://github.com/endatix/endatix/releases",
            },
            {
              label: "NuGet",
              href: "https://www.nuget.org/packages?q=endatix",
            },
            {
              label: "SurveyJS",
              href: "https://surveyjs.io",
            },
          ],
        },
        {
          title: "Community",
          items: [
            {
              label: "GitHub",
              href: "https://github.com/endatix",
            },
            {
              label: "Stack Overflow",
              href: "https://stackoverflow.com/questions/tagged/endatix",
            },
            {
              label: "Twitter",
              href: "https://x.com/endatix_",
            },
            {
              label: "Videos",
              href: "https://www.youtube.com/@endatix",
            },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} Endatix, Ltd. All rights reserved.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.oneDark,
      additionalLanguages: ["csharp", "json"],
    },
    colorMode: {
      defaultMode: "light",
      disableSwitch: false,
      respectPrefersColorScheme: true,
    },
    algolia: {
      appId: process.env.ALGOLIA_APP_ID || "YOUR_APP_ID",
      apiKey: process.env.ALGOLIA_API_KEY || "YOUR_API_KEY",
      indexName: process.env.ALGOLIA_INDEX_NAME || "Endatix Docs",
    },
  } satisfies Preset.ThemeConfig,
  markdown: {
    mermaid: true,
    hooks: {
      onBrokenMarkdownLinks: "warn",
      onBrokenMarkdownImages: "throw",
    },
  },
  themes: ["@docusaurus/theme-mermaid"],
  plugins: [
    // Shim gtag so plugin never throws when script is blocked or not loaded yet
    function gtagShimPlugin() {
      return {
        name: "gtag-shim",
        injectHtmlTags() {
          return {
            headTags: [
              {
                tagName: "script",
                innerHTML: "window.gtag=window.gtag||function(){};",
              },
            ],
          };
        },
      };
    },
  ],
};

export default config;
