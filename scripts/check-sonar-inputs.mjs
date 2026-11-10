#!/usr/bin/env node
// CI step for the SonarCloud end job (ci.yml): checks that this run still has every artifact `end`
// reads, from the API's list rather than the downloaded folders. Writes `analyze=true|false` to
// GITHUB_OUTPUT. A missing input fails the job, unless the latest earlier attempt's SonarCloud job
// passed for this commit (its cleanup deleted the inputs): then it skips with a notice.
//
// Usage: node scripts/check-sonar-inputs.mjs <artifact name> [...]
// Env:   GH_TOKEN (actions: read), GITHUB_REPOSITORY, GITHUB_RUN_ID, GITHUB_RUN_ATTEMPT, SONAR_JOB
//        (the end job's display name), GITHUB_OUTPUT, GITHUB_STEP_SUMMARY: set by the workflow
import { appendFileSync } from 'node:fs';
import { decideSonarRun } from './lib/sonar-inputs.mjs';

const { GH_TOKEN, GITHUB_REPOSITORY, GITHUB_RUN_ID, GITHUB_RUN_ATTEMPT, SONAR_JOB } = process.env;
const API = `https://api.github.com/repos/${GITHUB_REPOSITORY}/actions/runs/${GITHUB_RUN_ID}`;
const PAGE = 100;
const RETRY_MS = 5_000;
const TIMEOUT_MS = 60_000;
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

// Workflow commands end at a newline: escape anything from the API so it stays one annotation.
const escapeCommand = (text) => text.replaceAll('%', '%25').replaceAll('\r', '%0D').replaceAll('\n', '%0A');

async function getJson(url, deadline = Date.now() + TIMEOUT_MS) {
  const response = await fetch(url, {
    headers: { Authorization: `Bearer ${GH_TOKEN}`, Accept: 'application/vnd.github+json' },
  });
  // A brief GitHub 5xx should not turn the SonarCloud check red; ride it out until the deadline.
  if (response.status >= 500 && Date.now() + RETRY_MS < deadline) {
    await sleep(RETRY_MS);
    return getJson(url, deadline);
  }
  if (!response.ok) throw new Error(`GET ${url} returned ${response.status}: ${await response.text()}`);
  return response.json();
}

async function listAll(path, key) {
  const items = [];
  for (let page = 1; ; page++) {
    const batch = (await getJson(`${API}/${path}${path.includes('?') ? '&' : '?'}per_page=${PAGE}&page=${page}`))[key];
    items.push(...batch);
    if (batch.length < PAGE) return items;
  }
}

function append(file, line) {
  if (file) appendFileSync(file, `${line}\n`);
}

async function main() {
  const required = process.argv.slice(2);
  const [artifacts, jobs] = await Promise.all([listAll('artifacts', 'artifacts'), listAll('jobs?filter=all', 'jobs')]);
  const decision = decideSonarRun({
    required,
    present: artifacts.filter((a) => !a.expired).map((a) => a.name),
    attempt: Number(GITHUB_RUN_ATTEMPT),
    jobs,
    sonarJob: SONAR_JOB,
  });
  append(process.env.GITHUB_OUTPUT, `analyze=${decision.action === 'analyze'}`);
  if (decision.action === 'analyze') {
    console.log(`All ${required.length} inputs present: ${required.join(', ')}.`);
    return 0;
  }
  const missing = decision.missing.join(', ');
  if (decision.action === 'skip') {
    const text = `SonarCloud skipped: it passed for this commit on attempt ${decision.greenAttempt}, and its cleanup deleted the inputs (missing: ${missing}). Re-run all jobs to analyze again.`;
    console.log(`::notice title=SonarCloud::${escapeCommand(text)}`);
    append(process.env.GITHUB_STEP_SUMMARY, `ℹ️ ${text}`);
    return 0;
  }
  console.log(`::error title=SonarCloud::${escapeCommand(`Missing ${missing}: deleted after a passed analysis, or expired. Re-run all jobs.`)}`);
  return 1;
}

try {
  process.exitCode = await main();
} catch (error) {
  console.log(`::error title=SonarCloud::${escapeCommand(error.message)}`);
  process.exitCode = 1;
}
