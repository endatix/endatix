#!/usr/bin/env node
// CI step for the SonarCloud end job (ci.yml): checks that this run still has every artifact `end`
// reads, from the API's list rather than the downloaded folders. Writes `analyze=true|false` to
// GITHUB_OUTPUT. A missing input fails the job, unless an earlier attempt's analysis of this commit
// already passed (its cleanup deleted them): then it skips with a notice.
//
// Usage: node scripts/check-sonar-inputs.mjs <artifact name> [...]
// Env:   GH_TOKEN (actions: read), GITHUB_REPOSITORY, GITHUB_RUN_ID, GITHUB_RUN_ATTEMPT, SONAR_JOB
//        (the end job's display name), GITHUB_OUTPUT, GITHUB_STEP_SUMMARY: set by the workflow
import { appendFileSync } from 'node:fs';
import { decideSonarRun } from './lib/sonar-inputs.mjs';

const { GH_TOKEN, GITHUB_REPOSITORY, GITHUB_RUN_ID, GITHUB_RUN_ATTEMPT, SONAR_JOB } = process.env;
const API = `https://api.github.com/repos/${GITHUB_REPOSITORY}/actions/runs/${GITHUB_RUN_ID}`;

async function listAll(path, key) {
  const items = [];
  for (let page = 1; ; page++) {
    const response = await fetch(`${API}/${path}${path.includes('?') ? '&' : '?'}per_page=100&page=${page}`, {
      headers: { Authorization: `Bearer ${GH_TOKEN}`, Accept: 'application/vnd.github+json' },
    });
    if (!response.ok) throw new Error(`GET ${path} returned ${response.status}: ${await response.text()}`);
    const batch = (await response.json())[key];
    items.push(...batch);
    if (batch.length < 100) return items;
  }
}

function summary(line) {
  if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `${line}\n`);
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
  appendFileSync(process.env.GITHUB_OUTPUT, `analyze=${decision.action === 'analyze'}\n`);
  if (decision.action === 'analyze') {
    console.log(`All ${required.length} inputs present: ${required.join(', ')}.`);
    return 0;
  }
  const missing = decision.missing.join(', ');
  if (decision.action === 'skip') {
    const text = `SonarCloud skipped: attempt ${decision.greenAttempt} already analyzed this commit and passed, then deleted its inputs (missing: ${missing}). Re-run all jobs to analyze again.`;
    console.log(`::notice title=SonarCloud::${text}`);
    summary(`ℹ️ ${text}`);
    return 0;
  }
  console.log(`::error title=SonarCloud::Missing ${missing}: deleted after a passed analysis, or expired. Re-run all jobs.`);
  return 1;
}

try {
  process.exitCode = await main();
} catch (error) {
  console.log(`::error title=SonarCloud::${error.message.replaceAll('\n', ' ')}`);
  process.exitCode = 1;
}
