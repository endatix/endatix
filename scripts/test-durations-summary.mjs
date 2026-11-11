#!/usr/bin/env node
// CI step for the test jobs (ci.yml): writes each run's slowest tests to the job summary, so a
// slow test shows up on the run page instead of only in the raw log.
//
// Usage: node scripts/test-durations-summary.mjs <results dir> <title> [top=20] [--totals <file>]
//        (reads every *.ctrf under the dir, written by `--report-xunit-ctrf`)
//        --totals also writes the per-class totals as JSON, for the long-lived test-totals-* artifact
// Env:   GITHUB_STEP_SUMMARY   set by GitHub Actions; without it the Markdown goes to stdout
import { appendFileSync, existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { renderMarkdown, summarizeReports, totalsOf } from './lib/test-durations.mjs';

const args = process.argv.slice(2);
const totalsAt = args.indexOf('--totals');
const totalsFile = totalsAt === -1 ? undefined : args.splice(totalsAt, 2)[1];
const [dir, title, top = '20'] = args;
if (!dir || !title || (totalsAt !== -1 && !totalsFile)) {
  console.error('Usage: node scripts/test-durations-summary.mjs <results dir> <title> [top=20] [--totals <file>]');
  process.exit(2);
}

const files = existsSync(dir)
  ? readdirSync(dir, { recursive: true })
      .map(String)
      .filter((file) => file.endsWith('.ctrf'))
  : [];
if (files.length === 0) {
  // A run that crashed before reporting has no timings; the test step already failed for it.
  console.log(`::warning::No CTRF reports under ${dir}`);
  process.exit(0);
}

const reports = files.map((file) => JSON.parse(readFileSync(join(dir, file), 'utf8')));
const summary = summarizeReports(reports);
const markdown = renderMarkdown(summary, { title, top: Number(top) });
if (totalsFile) writeFileSync(totalsFile, JSON.stringify(totalsOf(summary)));

if (process.env.GITHUB_STEP_SUMMARY) {
  appendFileSync(process.env.GITHUB_STEP_SUMMARY, markdown);
} else {
  process.stdout.write(markdown);
}
