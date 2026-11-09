#!/usr/bin/env node
// CI step for the integration lanes (ci.yml): fails when a test the unsplit filter runs is in no
// lane, or in more than one. Lists tests only (no database), so it needs a built test project.
//
// Usage: node scripts/check-integration-lanes.mjs <project> <base filter> <lane filter>...
//        Each lane runs "<base filter>&<lane filter>", the same string the lane's test step uses.
import { execFileSync } from 'node:child_process';
import { compareLanes, parseTestList } from './lib/integration-lanes.mjs';

const [project, base, ...laneFilters] = process.argv.slice(2);
if (!project || !base || laneFilters.length < 2) {
  console.error('Usage: node scripts/check-integration-lanes.mjs <project> <base filter> <lane filter>...');
  process.exit(2);
}

// Throws on a non-zero exit, including 8 (the filter matched no tests).
const list = (filter) =>
  parseTestList(
    execFileSync('dotnet', ['test', project, '--no-build', '--list-tests', '--filter', filter], {
      encoding: 'utf8',
      maxBuffer: 64 * 1024 * 1024,
    }),
  );

const all = list(base);
const lanes = laneFilters.map((lane) => list(`${base}&${lane}`));
const { missing, extra } = compareLanes(all, lanes);

const counts = laneFilters.map((lane, i) => `${lane}: ${lanes[i].length}`).join(', ');
console.log(`${base}: ${all.length} tests. ${counts}.`);
for (const name of missing) console.log(`::error::In no lane: ${name}`);
for (const name of extra) console.log(`::error::In more than one lane: ${name}`);
if (missing.length > 0 || extra.length > 0) process.exit(1);
