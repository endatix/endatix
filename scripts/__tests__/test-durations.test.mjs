import assert from 'node:assert/strict';
import test from 'node:test';
import { renderMarkdown, summarizeReports } from '../lib/test-durations.mjs';

const report = (dll, tests, { start = 0, stop = 10_000, failed = 0, skipped = 0 } = {}) => ({
  results: {
    extra: { suites: [{ filePath: `/repo/tests/bin/Debug/net10.0/${dll}` }] },
    summary: { tests: tests.length, failed, skipped, start, stop },
    tests,
  },
});

test('summarizeReports names each test app by its assembly and orders them by wall time', () => {
  const summary = summarizeReports([
    report('Fast.Tests.dll', [], { stop: 1_000 }),
    report('Slow.Tests.dll', [], { stop: 9_000, failed: 1, skipped: 2 }),
  ]);

  assert.deepEqual(summary.assemblies, [
    { name: 'Slow.Tests', tests: 0, failed: 1, skipped: 2, durationMs: 9_000 },
    { name: 'Fast.Tests', tests: 0, failed: 0, skipped: 0, durationMs: 1_000 },
  ]);
});

test('renderMarkdown lists the slowest tests across all test apps, slowest first', () => {
  const summary = summarizeReports([
    report('A.Tests.dll', [
      { name: 'A.Quick', status: 'passed', duration: 100 },
      { name: 'A.Slow', status: 'passed', duration: 7_000 },
    ]),
    report('B.Tests.dll', [{ name: 'B.Middle', status: 'failed', duration: 2_900 }]),
  ]);

  const markdown = renderMarkdown(summary, { title: 'Unit tests', top: 2 });

  assert.match(markdown, /3 tests, 10\.0 s of test time\. The 2 slowest take 99% of it\./);
  assert.ok(markdown.indexOf('| A.Slow | passed | 7.0 s |') < markdown.indexOf('| B.Middle | failed | 2.9 s |'));
  assert.doesNotMatch(markdown, /A\.Quick/);
});

test('renderMarkdown escapes pipes in theory display names so the table row stays intact', () => {
  const summary = summarizeReports([
    report('A.Tests.dll', [{ name: 'A.Theory(input: "a|b")', status: 'passed', duration: 10 }]),
  ]);

  const markdown = renderMarkdown(summary, { title: 'Unit tests', top: 5 });

  assert.match(markdown, /\| A\.Theory\(input: "a\\\|b"\) \| passed \| 0\.0 s \|/);
});

test('renderMarkdown reports a zero share when no test time was recorded', () => {
  const markdown = renderMarkdown(summarizeReports([report('A.Tests.dll', [])]), { title: 'Empty', top: 5 });

  assert.match(markdown, /0 tests, 0\.0 s of test time\. The 0 slowest take 0% of it\./);
});
