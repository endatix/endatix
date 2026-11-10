#!/usr/bin/env node
// CI step for the integration lanes (ci.yml): fails when a test the unsplit filter runs is in no
// lane, or in more than one. Lists tests only (no database), so it needs a built test project.
//
// Usage: node scripts/check-integration-lanes.mjs <project> <base filter> <lane filter>...
//        Each lane runs "<base filter>&<lane filter>", the same string the lane's test step uses.
// Env:   DOTNET_ROOT   the SDK folder (actions/setup-dotnet sets it); dotnet is run from there,
//                      not looked up on PATH. Locally: DOTNET_ROOT="$(dirname "$(command -v dotnet)")"
import { execFileSync } from 'node:child_process';
import { join } from 'node:path';
import { compareLanes, parseTestList } from './lib/integration-lanes.mjs';

const [project, base, ...laneFilters] = process.argv.slice(2);
if (!project || !base || laneFilters.length < 2) {
  console.error('Usage: node scripts/check-integration-lanes.mjs <project> <base filter> <lane filter>...');
  process.exit(2);
}
if (!process.env.DOTNET_ROOT) {
  console.error('Set DOTNET_ROOT to the .NET SDK folder (actions/setup-dotnet sets it).');
  process.exit(2);
}
const dotnet = join(process.env.DOTNET_ROOT, 'dotnet');

// Throws on a non-zero exit, including 8 (the filter matched no tests).
const list = (filter) =>
  parseTestList(
    execFileSync(dotnet, ['test', project, '--no-build', '--list-tests', '--filter', filter], {
      encoding: 'utf8',
      maxBuffer: 64 * 1024 * 1024,
    }),
  );

const all = list(base);
if (all.length === 0) {
  // An empty parse would compare equal to empty lanes; fail instead (e.g. the list format changed).
  console.log(`::error::No test names parsed from --list-tests for ${base}`);
  process.exit(1);
}
const lanes = laneFilters.map((lane) => list(`${base}&${lane}`));
const { missing, extra } = compareLanes(all, lanes);

const counts = laneFilters.map((lane, i) => `${lane}: ${lanes[i].length}`).join(', ');
console.log(`${base}: ${all.length} tests. ${counts}.`);
for (const name of missing) console.log(`::error::In no lane: ${name}`);
for (const name of extra) console.log(`::error::In more than one lane: ${name}`);
if (missing.length > 0 || extra.length > 0) process.exit(1);
