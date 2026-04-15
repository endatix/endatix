import { readFile } from 'node:fs/promises';

function extractOutputValue(outputs, key) {
  const value = outputs?.[key]?.value;
  if (typeof value !== 'string' || value.length === 0) {
    throw new Error(
      `Deployment outputs must contain a non-empty string value for '${key}'.`,
    );
  }
  return value;
}

export async function readDeploymentOutputsFromFile(filePath) {
  const content = await readFile(filePath, 'utf8');
  const parsed = JSON.parse(content);

  return {
    hubBaseUrl: extractOutputValue(parsed, 'hubBaseUrl'),
    apiBaseUrl: extractOutputValue(parsed, 'apiBaseUrl'),
    nextPublicApiUrl: extractOutputValue(parsed, 'nextPublicApiUrl'),
    resourceGroupName: extractOutputValue(parsed, 'resourceGroupName'),
    apiAppName: extractOutputValue(parsed, 'apiAppName'),
    hubAppName: extractOutputValue(parsed, 'hubAppName'),
  };
}
