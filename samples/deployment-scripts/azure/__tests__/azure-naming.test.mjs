import assert from "node:assert/strict";
import test from "node:test";
import {
  buildNamingPreview,
  buildResourceNameOverridesBlock,
  cafName,
  cafStorageAccountName,
  hubCafAbbreviation,
  normalizeEnvironmentSegment,
  normalizeRegionAbbreviation,
  quickstartSegments,
} from "../lib/azure-naming.mjs";

const acmeSegments = {
  company: "acme",
  workload: "datanium",
  region: "eus",
  env: "test",
};

test("cafName builds CAF pattern with optional role", () => {
  assert.equal(cafName("app", acmeSegments), "app-acme-datanium-eus-test");
  assert.equal(
    cafName("nsg", acmeSegments, "app"),
    "nsg-acme-datanium-eus-test-app",
  );
  assert.equal(
    cafName("pip", acmeSegments, "vgw-01"),
    "pip-acme-datanium-eus-test-vgw-01",
  );
});

test("cafStorageAccountName concatenates without dashes and truncates to 24 chars", () => {
  assert.equal(cafStorageAccountName(acmeSegments), "stacmedataniumeustest");
  const longEnv = {
    ...acmeSegments,
    company: "verylongcompanyname",
    workload: "verylongworkloadname",
    env: "production",
  };
  assert.equal(cafStorageAccountName(longEnv).length, 24);
});

test("hubCafAbbreviation depends on hub deployment mode", () => {
  assert.equal(hubCafAbbreviation("static-site"), "stapp");
  assert.equal(hubCafAbbreviation("web-app"), "app");
});

test("buildNamingPreview uses stapp for static-site and app for web-app hub", () => {
  const staticPreview = buildNamingPreview({
    convention: "caf",
    segments: acmeSegments,
    hubDeploymentMode: "static-site",
  });
  const webPreview = buildNamingPreview({
    convention: "caf",
    segments: acmeSegments,
    hubDeploymentMode: "web-app",
  });

  const staticHub = staticPreview.find((row) => row.key === "hub");
  const webHub = webPreview.find((row) => row.key === "hub");

  assert.equal(staticHub?.example, "stapp-acme-datanium-eus-test");
  assert.equal(webHub?.example, "app-acme-datanium-eus-test");
});

test("quickstartSegments uses project as company with default workload and region", () => {
  const segments = quickstartSegments("MyProject", "sandbox");
  assert.equal(segments.company, "myproject");
  assert.equal(segments.workload, "endatix");
  assert.equal(segments.region, "weu");
  assert.equal(segments.env, "sandbox");
});

test("normalizeRegionAbbreviation strips invalid chars and caps length", () => {
  assert.equal(normalizeRegionAbbreviation("WEU"), "weu");
  assert.equal(normalizeRegionAbbreviation("east-us-2"), "eastus");
  assert.equal(normalizeRegionAbbreviation(""), "weu");
});

test("normalizeEnvironmentSegment lowercases and hyphenates", () => {
  assert.equal(normalizeEnvironmentSegment("Sandbox"), "sandbox");
  assert.equal(normalizeEnvironmentSegment("  prod env  "), "prod-env");
});

test("buildResourceNameOverridesBlock includes auto hints and segment comment", () => {
  const block = buildResourceNameOverridesBlock({
    convention: "quickstart",
    segments: quickstartSegments("endatix", "sandbox"),
    hubDeploymentMode: "static-site",
  });

  assert.match(block, /Naming mode: quickstart/);
  assert.match(block, /param apiAppNameOverride = ''/);
  assert.match(block, /\/\/ auto: app-endatix-endatix-weu-sandbox/);
  assert.match(block, /\/\/ auto: stapp-endatix-endatix-weu-sandbox/);
  assert.match(block, /Managed VNet also creates:/);
});
