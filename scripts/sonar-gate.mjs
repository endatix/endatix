#!/usr/bin/env node
// CI gate step for the SonarCloud job (ci.yml). Waits for the analysis the scanner just
// uploaded, then fails the job only on blocking conditions; advisory ones become warnings.
//
// Usage: node scripts/sonar-gate.mjs   (reads .sonarqube/out/.sonar/report-task.txt)
// Env:   SONAR_TOKEN               the scanner's token, sent to SONAR_HOST only
//        SONAR_ADVISORY_METRICS    comma-separated metric keys that warn instead of failing
//                                  (default: new_duplicated_lines_density)
//        GITHUB_STEP_SUMMARY       set by GitHub Actions; the verdict table goes there
import { appendFileSync, readFileSync } from 'node:fs';
import { describeCondition, evaluateGate, parseReportTask, sonarApiUrl } from './lib/sonar-gate.mjs';

// Fixed, not read from the report file: the token goes to SonarCloud and nowhere else.
const SONAR_HOST = 'https://sonarcloud.io';
const REPORT_TASK = '.sonarqube/out/.sonar/report-task.txt';
const ADVISORY = (process.env.SONAR_ADVISORY_METRICS ?? 'new_duplicated_lines_density')
  .split(',')
  .map((metric) => metric.trim())
  .filter(Boolean);
const POLL_MS = 5_000;
const TIMEOUT_MS = 300_000;
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

// Workflow commands end at a newline: escape anything from the report or the API so it stays one annotation.
const escapeCommand = (text) => text.replaceAll('%', '%25').replaceAll('\r', '%0D').replaceAll('\n', '%0A');

async function getJson(url, deadline = Date.now() + TIMEOUT_MS) {
  const headers = process.env.SONAR_TOKEN ? { Authorization: `Bearer ${process.env.SONAR_TOKEN}` } : {};
  const response = await fetch(url, { headers });
  // SonarCloud has brief 5xx hiccups; sonar.qualitygate.wait rode them out, so we do too.
  if (response.status >= 500 && Date.now() + POLL_MS < deadline) {
    await sleep(POLL_MS);
    return getJson(url, deadline);
  }
  if (!response.ok) {
    throw new Error(`GET ${url} returned ${response.status}: ${await response.text()}`);
  }
  return response.json();
}

async function waitForAnalysis(ceTaskUrl, deadline = Date.now() + TIMEOUT_MS) {
  if (Date.now() >= deadline) {
    throw new Error(`SonarCloud analysis did not finish within ${TIMEOUT_MS / 1000}s: ${ceTaskUrl}`);
  }
  const { task } = await getJson(ceTaskUrl, deadline);
  if (task.status === 'SUCCESS') return task.analysisId;
  if (task.status === 'FAILED' || task.status === 'CANCELED') {
    throw new Error(`SonarCloud analysis ${task.status}: ${task.errorMessage ?? 'no message'}`);
  }
  await sleep(POLL_MS);
  return waitForAnalysis(ceTaskUrl, deadline);
}

function report(verdict, dashboardUrl) {
  for (const c of verdict.advisory) {
    const text = `${describeCondition(c)}. Fix it later; it does not fail the build. ${dashboardUrl}`;
    console.log(`::warning title=SonarCloud (advisory)::${escapeCommand(text)}`);
  }
  for (const c of verdict.blocking) {
    const text = `${describeCondition(c)}. ${dashboardUrl}`;
    console.log(`::error title=SonarCloud quality gate::${escapeCommand(text)}`);
  }
  if (!process.env.GITHUB_STEP_SUMMARY) return;
  const rows = [...verdict.blocking.map((c) => ['❌ blocking', c]), ...verdict.advisory.map((c) => ['⚠️ advisory', c])]
    .map(([kind, c]) => `| ${kind} | ${describeCondition(c)} |`);
  const verdictLine = verdict.passed ? '✅ passed' : '❌ failed';
  appendFileSync(
    process.env.GITHUB_STEP_SUMMARY,
    [`### SonarCloud gate: ${verdictLine}`, '', ...(rows.length ? ['| | Condition |', '|---|---|', ...rows, ''] : []), `[Dashboard](${dashboardUrl})`, ''].join('\n'),
  );
}

async function main() {
  const task = parseReportTask(readFileSync(REPORT_TASK, 'utf8'));
  const analysisId = await waitForAnalysis(sonarApiUrl(SONAR_HOST, '/api/ce/task', { id: task.ceTaskId }));
  const { projectStatus } = await getJson(sonarApiUrl(SONAR_HOST, '/api/qualitygates/project_status', { analysisId }));
  const verdict = evaluateGate(projectStatus, ADVISORY);
  report(verdict, task.dashboardUrl);
  console.log(`SonarCloud gate ${verdict.passed ? 'passed' : 'failed'} (advisory: ${ADVISORY.join(', ') || 'none'}).`);
  return verdict.passed ? 0 : 1;
}

try {
  process.exitCode = await main();
} catch (error) {
  console.log(`::error title=SonarCloud gate::${escapeCommand(error.message)}`);
  process.exitCode = 1;
}
