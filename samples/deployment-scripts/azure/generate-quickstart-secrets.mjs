#!/usr/bin/env node

import { access, mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createInterface } from "node:readline";
import {
  applyStringParamReplacements,
  readStringParamFromBicepParam,
} from "../../../scripts/lib/bicepparam-utils.mjs";
import {
  formatCommandSnippet,
  tipText,
  infoText,
  errorText,
  printNextSteps,
} from "../../../scripts/lib/console-format.mjs";
import {
  randomBase64,
  randomHex,
  randomSigningKey,
} from "../../../scripts/lib/secret-generation.mjs";
import { readDeploymentOutputsFromFile } from "./lib/deployment-outputs.mjs";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const bicepParametersPath = path.join(__dirname, "parameters.bicepparam");
const localParametersPath = path.join(
  __dirname,
  "parameters.production.bicepparam",
);
const deploymentOutputsPath = path.join(__dirname, "deployment-outputs.json");

class UserFacingError extends Error {
  constructor(message) {
    super(message);
    this.name = "UserFacingError";
  }
}

async function fileExists(filePath) {
  try {
    await access(filePath);
    return true;
  } catch {
    return false;
  }
}

async function getHubDeployEnvPath() {
  // Try 4 levels up if adjacent: endatix/endatix-api/samples/deployment-scripts/azure -> endatix/endatix-hub
  let hubPath = path.join(__dirname, "..", "..", "..", "..", "endatix-hub");
  if (!(await fileExists(hubPath))) {
    // Fallback: 4 levels up if inside submodules: endatix/oss/samples/deployment-scripts/azure -> endatix/hub
    const fallbackPath = path.join(__dirname, "..", "..", "..", "..", "hub");
    if (await fileExists(fallbackPath)) {
      hubPath = fallbackPath;
    }
  }
  return path.join(hubPath, ".env.production");
}

function deriveResourceGroupName(environmentName, projectName = "endatix") {
  const normalizedEnv = (environmentName ?? "temp").toLowerCase();
  const normalizedProject = normalizeProjectName(projectName);
  return `rg-${normalizedProject}-${normalizedEnv}-us`;
}

function normalizeResourcePrefix(resourcePrefix) {
  return resourcePrefix.endsWith("-") ? resourcePrefix : `${resourcePrefix}-`;
}

function normalizeProjectName(projectName) {
  const normalized = (projectName ?? "")
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9-]/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "");

  return normalized || "endatix";
}

const rl = createInterface({
  input: process.stdin,
  output: process.stdout,
});

function question(query) {
  return new Promise((resolve) => rl.question(query, resolve));
}

async function resolveSecretGenerationMode() {
  let isReadOnly = process.argv.includes("--read-only");
  let isForce = process.argv.includes("--force");
  let skipSecretGen = false;

  if (!(await fileExists(localParametersPath))) {
    return { isReadOnly, isForce, skipSecretGen };
  }

  if (isReadOnly) {
    return { isReadOnly, isForce, skipSecretGen: true };
  }

  if (isForce) {
    return { isReadOnly, isForce, skipSecretGen };
  }

  console.log(
    `${infoText("Found existing")} ${path.basename(localParametersPath)}`,
  );
  const answer = await question(
    "Do you want to [O]verwrite it, [R]euse values (Read-only mode), or [C]ancel? (O/R/C) [R]: ",
  );
  const choice = answer.trim().toLowerCase();

  if (choice === "o") {
    isForce = true;
  } else if (choice === "c") {
    console.log("Operation cancelled.");
    process.exit(0);
  } else {
    isReadOnly = true;
    skipSecretGen = true;
  }

  return { isReadOnly, isForce, skipSecretGen };
}

async function loadDeploymentConfig() {
  const baseBicepParameters = await readFile(bicepParametersPath, "utf8");
  const configuredPrefix =
    readStringParamFromBicepParam(baseBicepParameters, "resourcePrefix") ??
    "eval-";
  const environmentName =
    readStringParamFromBicepParam(baseBicepParameters, "environment") ?? "temp";
  const projectName =
    readStringParamFromBicepParam(baseBicepParameters, "project") ?? "endatix";

  return {
    baseBicepParameters,
    resourcePrefix: normalizeResourcePrefix(configuredPrefix),
    environmentName,
    projectName: normalizeProjectName(projectName),
  };
}

async function resolveResourceGroupInfo(environmentName, projectName) {
  const hasRg = await question(
    `\nDo you have an existing Azure resource group? (y/N): `,
  );

  if (hasRg.trim().toLowerCase().startsWith("y")) {
    let resourceGroupName = await question("❯ Enter the resource group name: ");

    if (!resourceGroupName.trim()) {
      resourceGroupName = deriveResourceGroupName(environmentName, projectName);
      console.log(`Using derived name: ${resourceGroupName}`);
    }

    return {
      resourceGroupName: resourceGroupName.trim(),
      rgLocation: "centralus",
      createRgCmd: "",
    };
  }

  const resourceGroupName = deriveResourceGroupName(environmentName, projectName);
  let rgLocation = "centralus";

  console.log(
    `\n${tipText("Need a location? Run this to see all Azure locations:")}`,
  );
  console.log(
    formatCommandSnippet(
      'az account list-locations --query "[*].name" --out tsv | sort',
    ),
  );

  const enteredLoc = await question(
    `❯ Enter Azure location to create the RG in [default: ${rgLocation}]: `,
  );
  if (enteredLoc.trim()) {
    rgLocation = enteredLoc.trim();
  }

  return {
    resourceGroupName,
    rgLocation,
    createRgCmd: `az group create --name ${resourceGroupName} --location ${rgLocation}`,
  };
}

async function resolveProjectForCurrentRun(baseBicepParameters, skipSecretGen) {
  const configuredProject =
    readStringParamFromBicepParam(baseBicepParameters, "project") ?? "endatix";
  const defaultProject = normalizeProjectName(configuredProject);

  if (skipSecretGen) {
    return { effectiveProject: defaultProject, projectOverride: null };
  }

  const overrideProject = await question(
    `Override project name for this run? (y/N): `,
  );

  if (!overrideProject.trim().toLowerCase().startsWith("y")) {
    return { effectiveProject: defaultProject, projectOverride: null };
  }

  const enteredProject = await question(
    `❯ Enter project name [default: ${defaultProject}]: `,
  );
  const normalizedProject = normalizeProjectName(
    enteredProject.trim() || defaultProject,
  );
  console.log(`Using normalized project: ${normalizedProject}`);
  return {
    effectiveProject: normalizedProject,
    projectOverride: normalizedProject,
  };
}

async function ensureLocalParameters(baseBicepParameters, skipSecretGen, projectOverride) {
  if (skipSecretGen) {
    console.log(
      `\n\u2705 Reusing values from: ${path.basename(localParametersPath)}`,
    );
    return;
  }

  const authSecret = randomBase64(32);
  const sessionSecret = randomHex(32);
  const nextServerActionsEncryptionKey = randomBase64(32);
  const endatixJwtSigningKey = randomSigningKey(64);
  const submissionsAccessTokenSigningKey = randomSigningKey(64);
  const postgresAdminPassword = randomSigningKey(24);
  const initialUserPassword = randomSigningKey(24);

  const generatedReplacements = [
    ["postgresAdminPassword", postgresAdminPassword],
    ["initialUserPassword", initialUserPassword],
    ["endatixJwtSigningKey", endatixJwtSigningKey],
    ["submissionsAccessTokenSigningKey", submissionsAccessTokenSigningKey],
    ["hubSessionSecret", sessionSecret],
    ["hubAuthSecret", authSecret],
    ["nextServerActionsEncryptionKey", nextServerActionsEncryptionKey],
  ];

  if (projectOverride) {
    generatedReplacements.push(["project", projectOverride]);
  }

  const localParametersBicep = applyStringParamReplacements(
    baseBicepParameters,
    generatedReplacements,
  );

  await writeFile(localParametersPath, localParametersBicep, "utf8");
  console.log(
    `\n\u2705 Generated secure parameters dynamically in: ${path.basename(localParametersPath)}`,
  );
}

async function interactiveWizard() {
  console.log(`\n${infoText("✦ Welcome to the Endatix Azure Quickstart! ✦")}`);
  console.log(
    "This wizard will help you configure your deployment and generate commands.\n",
  );

  const { skipSecretGen } = await resolveSecretGenerationMode();
  const { baseBicepParameters, environmentName, projectName } =
    await loadDeploymentConfig();
  const { effectiveProject, projectOverride } = await resolveProjectForCurrentRun(
    baseBicepParameters,
    skipSecretGen,
  );
  const { resourceGroupName, createRgCmd } =
    await resolveResourceGroupInfo(environmentName, effectiveProject || projectName);

  await ensureLocalParameters(baseBicepParameters, skipSecretGen, projectOverride);

  console.log(`\n${infoText("=== Step 1: Provision Infrastructure ===")}`);

  const deployParamsFile = path.basename(localParametersPath);
  const deployOutputsFile = path.basename(deploymentOutputsPath);
  const deployInfraCommand = [
    "az deployment group create",
    `--resource-group ${resourceGroupName}`,
    `--parameters ${deployParamsFile}`,
    "--mode Complete",
    `--query properties.outputs -o json > ${deployOutputsFile}`,
  ].join(" ");

  printNextSteps(
    [
      `1) Review generated parameters file (${deployParamsFile}) and adjust if needed.`,
      createRgCmd
        ? "2) Create resource group (skip if it already exists):"
        : null,
      createRgCmd ? formatCommandSnippet(createRgCmd) : null,
      `${createRgCmd ? "3" : "2"}) Provision the resources:\n${formatCommandSnippet(deployInfraCommand)}`,
    ].filter(Boolean),
  );

  console.log(`\n${infoText("⏳ Action Required:")}`);
  console.log(`Please run the Azure commands above in another terminal.`);
  await question(
    `Press [Enter] here ONLY once the deployment completes successfully to continue...`,
  );

  const outputs = await readDeploymentOutputsFromFile(deploymentOutputsPath);
  const parametersDeployContent = await readFile(localParametersPath, "utf8");
  const sessionSecret = readStringParamFromBicepParam(
    parametersDeployContent,
    "hubSessionSecret",
  );

  if (!sessionSecret) {
    throw new UserFacingError(
      `Unable to read 'hubSessionSecret' from '${localParametersPath}'.`,
    );
  }

  const envDeployContent = [
    "# Generated by generate-quickstart-secrets.mjs",
    "# Build-time values only. Runtime secrets/URLs come from Azure app settings via Bicep.",
    `ENDATIX_BASE_URL=${outputs.apiBaseUrl}`,
    `SESSION_SECRET=${sessionSecret}`,
    `NEXT_PUBLIC_API_URL=${outputs.nextPublicApiUrl}`,
    "NEXT_PUBLIC_ENVIRONMENT=production",
    "NEXT_PUBLIC_IS_DEBUG_MODE=false",
    "NEXT_FORMS_COOKIE_NAME=FPSK",
    "NEXT_FORMS_COOKIE_DURATION_DAYS=7",
    "",
  ].join("\n");

  const hubDeployEnvPath = await getHubDeployEnvPath();
  await mkdir(path.dirname(hubDeployEnvPath), { recursive: true });
  await writeFile(hubDeployEnvPath, envDeployContent, "utf8");

  const deployApiCommand = [
    "az webapp deploy",
    `--resource-group ${resourceGroupName}`,
    `--name ${outputs.apiAppName}`,
    "--src-path endatix-api.zip",
    "--type zip",
  ].join(" ");

  const deployHubCommand = [
    "swa deploy",
    `--resource-group ${resourceGroupName}`,
    `--app-name ${outputs.hubAppName}`,
    "--env production",
    "--api-language node",
    "--api-version 22",
  ].join(" ");

  console.log(
    `\n✅ Successfully generated ${path.basename(hubDeployEnvPath)}!`,
  );
  console.log(`📍 Path: ${hubDeployEnvPath}`);

  console.log(`\n${infoText("=== Step 2: Build & Deploy Apps ===")}`);

  console.log(`\n${infoText("- Deploy Endatix Hub")}`);
  console.log(
    `  1. cd into your endatix-hub directory (make sure ${path.basename(hubDeployEnvPath)} is in the root)`,
  );
  console.log(`  2. Build the app standalone:`);
  console.log(formatCommandSnippet("pnpm build:standalone"));
  console.log(`  3. Deploy the Next.js app to Azure:`);
  console.log(formatCommandSnippet(deployHubCommand));

  console.log(`\n${infoText("- Deploy Endatix API")}`);
  console.log(`  1. Return to your endatix root directory`);
  console.log(`  2. Publish the API:`);
  console.log(
    formatCommandSnippet(
      "dotnet publish src/Endatix.WebHost -c Release -o ./publish",
    ),
  );
  console.log(`  3. Compress the published API:`);
  console.log(
    formatCommandSnippet("cd publish && zip -r ../endatix-api.zip . && cd .."),
  );
  console.log(`  4. Deploy the zip to Azure App Service:`);
  console.log(formatCommandSnippet(deployApiCommand));

  console.log(
    `\n${infoText("✨ All Done! Check out your Hub at:")} ${outputs.hubBaseUrl}\n`,
  );

  rl.close();
}

async function main() {
  await interactiveWizard();
}

try {
  await main();
} catch (error) {
  if (error instanceof UserFacingError) {
    console.error(`\n${errorText("[ERROR]")} ${error.message}`);
  } else {
    console.error(`\n${errorText("✖")} Failed to generate quickstart secrets.`);
    console.error(`${errorText("[ERROR]")} ${error}`);
  }
  process.exit(1);
}
