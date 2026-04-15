import assert from 'node:assert/strict';
import { mkdtemp, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { readDeploymentOutputsFromFile } from '../lib/deployment-outputs.mjs';

test('readDeploymentOutputsFromFile parses required output values', async () => {
  const tempDir = await mkdtemp(path.join(os.tmpdir(), 'endatix-outputs-'));
  const outputsPath = path.join(tempDir, 'outputs.json');
  const json = {
    hubBaseUrl: { type: 'String', value: 'https://eval-endatix-hub.azurestaticapps.net' },
    apiBaseUrl: { type: 'String', value: 'https://eval-endatix-api.azurewebsites.net' },
    nextPublicApiUrl: { type: 'String', value: 'https://eval-endatix-api.azurewebsites.net/api' },
    resourceGroupName: { type: 'String', value: 'rg-endatix-eval-us' },
    apiAppName: { type: 'String', value: 'eval-endatix-api' },
    hubAppName: { type: 'String', value: 'eval-endatix-hub' },
  };

  await writeFile(outputsPath, JSON.stringify(json), 'utf8');
  const outputs = await readDeploymentOutputsFromFile(outputsPath);

  assert.equal(outputs.hubBaseUrl, json.hubBaseUrl.value);
  assert.equal(outputs.apiBaseUrl, json.apiBaseUrl.value);
  assert.equal(outputs.nextPublicApiUrl, json.nextPublicApiUrl.value);
  assert.equal(outputs.resourceGroupName, json.resourceGroupName.value);
  assert.equal(outputs.apiAppName, json.apiAppName.value);
  assert.equal(outputs.hubAppName, json.hubAppName.value);
});

test('readDeploymentOutputsFromFile throws when required key is missing', async () => {
  const tempDir = await mkdtemp(path.join(os.tmpdir(), 'endatix-outputs-'));
  const outputsPath = path.join(tempDir, 'outputs.json');
  const json = {
    apiBaseUrl: { type: 'String', value: 'https://eval-endatix-api.azurewebsites.net' },
    nextPublicApiUrl: { type: 'String', value: 'https://eval-endatix-api.azurewebsites.net/api' },
    resourceGroupName: { type: 'String', value: 'rg-endatix-eval-us' },
    apiAppName: { type: 'String', value: 'eval-endatix-api' },
    hubAppName: { type: 'String', value: 'eval-endatix-hub' },
  };

  await writeFile(outputsPath, JSON.stringify(json), 'utf8');
  await assert.rejects(
    () => readDeploymentOutputsFromFile(outputsPath),
    /hubBaseUrl/,
  );
});
