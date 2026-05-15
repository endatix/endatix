import { normalizeProjectName } from "../../../../scripts/lib/project-name-utils.mjs";

export const NAMING_CONVENTIONS = ["quickstart", "caf", "manual"];

export const DEFAULT_WORKLOAD = "endatix";
export const DEFAULT_REGION = "weu";

/** CAF pattern: {abbr}-{company}-{workload}-{region}-{env}[-{role}] */
export function normalizeNamingSegment(value) {
  return normalizeProjectName(value);
}

export function normalizeRegionAbbreviation(value) {
  const raw = String(value ?? "")
    .trim()
    .toLowerCase()
    .replaceAll(/[^a-z0-9]/g, "");
  return raw.slice(0, 6) || DEFAULT_REGION;
}

export function normalizeEnvironmentSegment(value) {
  return (
    String(value ?? "dev")
      .trim()
      .toLowerCase()
      .replaceAll(/[^a-z0-9]+/g, "-")
      .replace(/^-+|-+$/g, "") || "dev"
  );
}

/**
 * @param {object} segments
 * @param {string} segments.company
 * @param {string} segments.workload
 * @param {string} segments.region
 * @param {string} segments.env
 */
export function cafName(abbr, segments, role = "") {
  const company = normalizeNamingSegment(segments.company);
  const workload = normalizeNamingSegment(segments.workload);
  const region = normalizeRegionAbbreviation(segments.region);
  const env = normalizeEnvironmentSegment(segments.env);
  const base = `${abbr}-${company}-${workload}-${region}-${env}`;
  return role ? `${base}-${role}` : base;
}

/**
 * @param {object} segments
 */
export function cafStorageAccountName(segments) {
  const company = normalizeNamingSegment(segments.company).replaceAll("-", "");
  const workload = normalizeNamingSegment(segments.workload).replaceAll(
    "-",
    "",
  );
  const region = normalizeRegionAbbreviation(segments.region);
  const env = normalizeEnvironmentSegment(segments.env).replaceAll("-", "");
  const name = `st${company}${workload}${region}${env}`;
  return name.slice(0, 24);
}

/**
 * @param {'static-site' | 'web-app'} hubDeploymentMode
 */
export function hubCafAbbreviation(hubDeploymentMode) {
  return hubDeploymentMode === "web-app" ? "app" : "stapp";
}

/**
 * @param {object} options
 * @param {'static-site' | 'web-app'} options.hubDeploymentMode
 */
export function getResourceNameSpecs({
  hubDeploymentMode = "static-site",
} = {}) {
  const hubAbbr = hubCafAbbreviation(hubDeploymentMode);
  return [
    { key: "api", label: "API (App Service)", abbr: "app", role: "" },
    { key: "hub", label: "Hub (SWA / Web App)", abbr: hubAbbr, role: "" },
    { key: "plan", label: "App Service plan", abbr: "plan", role: "" },
    { key: "appi", label: "Application Insights", abbr: "appi", role: "" },
    { key: "log", label: "Log Analytics workspace", abbr: "log", role: "" },
    { key: "psql", label: "PostgreSQL server", abbr: "psql", role: "" },
    {
      key: "storage",
      label: "Storage account",
      abbr: "st",
      role: "",
      isStorage: true,
    },
    { key: "vnet", label: "VNet (managed)", abbr: "vnet", role: "" },
    { key: "nsgApp", label: "App NSG (managed)", abbr: "nsg", role: "app" },
    { key: "nsgDb", label: "DB NSG (managed)", abbr: "nsg", role: "db" },
    { key: "vgw", label: "VPN gateway (managed)", abbr: "vgw", role: "01" },
    {
      key: "pip",
      label: "Gateway public IP (managed)",
      abbr: "pip",
      role: "vgw-01",
    },
  ];
}

/**
 * @param {object} options
 * @param {'quickstart' | 'caf' | 'manual'} options.convention
 * @param {object} options.segments
 * @param {'static-site' | 'web-app'} [options.hubDeploymentMode]
 */
export function buildNamingPreview({
  convention,
  segments,
  hubDeploymentMode = "static-site",
}) {
  const specs = getResourceNameSpecs({ hubDeploymentMode });
  return specs.map((spec) => ({
    key: spec.key,
    label: spec.label,
    example: spec.isStorage
      ? cafStorageAccountName(segments)
      : cafName(spec.abbr, segments, spec.role),
    convention,
  }));
}

export function formatNamingPreviewTable(previewRows) {
  const maxLabel = Math.max(...previewRows.map((r) => r.label.length), 5);
  return previewRows
    .map((row) => `  ${row.label.padEnd(maxLabel)}  ${row.example}`)
    .join("\n");
}

const OVERRIDE_PARAM_BY_KEY = {
  api: "apiAppNameOverride",
  hub: "hubAppNameOverride",
  plan: "appServicePlanNameOverride",
  appi: "appInsightsNameOverride",
  log: "logAnalyticsWorkspaceNameOverride",
  psql: "postgresqlServerNameOverride",
  storage: "storageAccountNameOverride",
  vnet: "vnetNameOverride",
};

/**
 * @param {object} options
 * @param {'quickstart' | 'caf' | 'manual'} options.convention
 * @param {object} options.segments
 * @param {'static-site' | 'web-app'} [options.hubDeploymentMode]
 */
export function buildResourceNameOverridesBlock({
  convention,
  segments,
  hubDeploymentMode = "static-site",
}) {
  const preview = buildNamingPreview({
    convention,
    segments,
    hubDeploymentMode,
  });
  const previewByKey = Object.fromEntries(preview.map((row) => [row.key, row]));

  const conventionNote =
    convention === "manual"
      ? "// Manual: set any *Override below; empty values use CAF auto names from segment params in this file."
      : `// Naming mode: ${convention} — pattern {abbr}-{company}-{workload}-{region}-{env}`;

  const segmentLine = `// Segments: company=${normalizeNamingSegment(segments.company)}, workload=${normalizeNamingSegment(segments.workload)}, region=${normalizeRegionAbbreviation(segments.region)}, env=${normalizeEnvironmentSegment(segments.env)}`;

  const overrideLines = [
    "api",
    "hub",
    "plan",
    "appi",
    "log",
    "psql",
    "storage",
  ].map((key) => {
    const param = OVERRIDE_PARAM_BY_KEY[key];
    const example = previewByKey[key]?.example ?? "";
    const pad = Math.max(28 - param.length, 1);
    const storageNote =
      key === "storage"
        ? " (3-24 chars, lowercase alphanumeric, no dashes)"
        : "";
    return `param ${param} = ''${" ".repeat(pad)}// auto: ${example}${storageNote}`;
  });

  const networkHint = `// Managed VNet also creates: ${previewByKey.nsgApp?.example}, ${previewByKey.nsgDb?.example}, ${previewByKey.vgw?.example}, ${previewByKey.pip?.example}`;

  return [
    "",
    "// --- Resource name overrides ---",
    conventionNote,
    segmentLine,
    "// Azure abbreviations: https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations",
    "// -------------------------------------------------------------------",
    ...overrideLines,
    `param vnetNameOverride = ''${" ".repeat(22)}// auto: ${previewByKey.vnet?.example} (managed VNet only)`,
    networkHint,
    "",
  ].join("\n");
}

/**
 * Quickstart segments: company = project, default workload/region.
 */
export function quickstartSegments(project, environment) {
  return {
    company: normalizeNamingSegment(project),
    workload: DEFAULT_WORKLOAD,
    region: DEFAULT_REGION,
    env: normalizeEnvironmentSegment(environment),
  };
}
