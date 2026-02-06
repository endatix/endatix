import type { SidebarsConfig } from "@docusaurus/plugin-content-docs";

/**
 * Master Switch: Developers | End Users.
 * Building Your Solution and Configuration use explicit categories so the root index
 * is only at parent level (no duplicate) and subcategories (Authentication, Authorization,
 * Deployment; Settings) keep their third-level hierarchy.
 */
const sidebars: SidebarsConfig = {
  devSidebar: [
    {
      type: "category",
      label: "Getting Started",
      collapsed: false,
      link: {
        type: "generated-index",
        slug: "getting-started",
        title: "Getting Started",
      },
      items: [
        "getting-started/quick-start",
        "getting-started/setup-nuget-package",
        "getting-started/setup-repository",
        "getting-started/what-is-endatix",
        "getting-started/architecture",
      ],
    },
    {
      type: "category",
      label: "Guides",
      collapsed: true,
      link: { type: "generated-index", slug: "guides", title: "Guides" },
      items: [
        "guides/webhooks",
        "guides/docker-setup",
        "guides/session-bridge",
        "guides/external-authorization",
        "guides/api-permissions-reference",
        "guides/form-prefilling",
        "guides/json-to-jsonb-migration",
      ],
    },
    {
      type: "category",
      label: "Building Your Solution",
      collapsed: true,
      link: { type: "doc", id: "building-your-solution/index" },
      items: [
        {
          type: "category",
          label: "Authentication",
          link: {
            type: "doc",
            id: "building-your-solution/authentication/index",
          },
          collapsed: true,
          items: [
            "building-your-solution/authentication/endatix-jwt",
            "building-your-solution/authentication/google-oauth",
            "building-your-solution/authentication/keycloak",
          ],
        },
        {
          type: "category",
          label: "Authorization",
          link: {
            type: "doc",
            id: "building-your-solution/authorization/index",
          },
          collapsed: true,
          items: ["building-your-solution/authorization/keycloak-rbac"],
        },
        {
          type: "category",
          label: "Deployment",
          collapsed: true,
          items: ["building-your-solution/deployment/subfolder-deployment"],
        },
      ],
    },
    {
      type: "category",
      label: "Configuration",
      collapsed: true,
      link: { type: "doc", id: "configuration/index" },
      items: [
        "configuration/api-configuration",
        "configuration/data-configuration",
        "configuration/infrastructure-configuration",
        "configuration/security-configuration",
        {
          type: "category",
          label: "Settings",
          link: { type: "doc", id: "configuration/settings/index" },
          collapsed: true,
          items: [
            "configuration/settings/auth-settings",
            "configuration/settings/cors-settings",
            "configuration/settings/data-settings",
            "configuration/settings/health-checks",
            "configuration/settings/persistence-settings",
          ],
        },
      ],
    },
    {
      type: "html",
      value: "PRODUCTS",
      className: "sidebar-products-divider",
    },
    {
      type: "category",
      label: "Endatix API",
      collapsed: true,
      items: [
        "developers/api/index",
        {
          type: "link",
          label: "API Reference",
          href: "/docs/developers/api/api-reference",
        },
      ],
    },
    {
      type: "category",
      label: "Endatix Hub",
      collapsed: true,
      items: ["developers/hub/index"],
    },
  ],
  userSidebar: [
    {
      type: "category",
      label: "Forms",
      collapsed: false,
      link: {
        type: "generated-index",
        slug: "forms",
        title: "Forms",
      },
      items: [
        {
          type: "category",
          label: "Form Builder",
          collapsed: false,
          link: {
            type: "generated-index",
            slug: "form-builder",
            title: "Form Builder",
          },
          items: [
            "end-users/forms/form-builder/question-loops"
          ],
        }
      ],
    }
  ],
};

export default sidebars;
