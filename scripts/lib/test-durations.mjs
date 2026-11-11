// Reads xUnit CTRF reports (`--report-xunit-ctrf`) and renders the job-summary Markdown for
// scripts/test-durations-summary.mjs. One report per test app.

const SLOW_TEST_MS = 5_000;

const assemblyOf = (report) => {
  const filePath = report.results?.extra?.suites?.[0]?.filePath ?? '';
  return filePath.split(/[\\/]/).pop()?.replace(/\.dll$/, '') || 'unknown';
};

const seconds = (ms) => `${(ms / 1000).toFixed(1)} s`;

// Pipes and newlines in a test name would break the Markdown table row.
const cell = (text) => String(text).replaceAll('|', String.raw`\|`).replaceAll(/\r?\n/g, ' ');

export function summarizeReports(reports) {
  const assemblies = reports
    .map((report) => {
      const summary = report.results?.summary ?? {};
      return {
        name: assemblyOf(report),
        tests: summary.tests ?? 0,
        failed: summary.failed ?? 0,
        skipped: summary.skipped ?? 0,
        durationMs: (summary.stop ?? 0) - (summary.start ?? 0),
      };
    })
    .sort((a, b) => b.durationMs - a.durationMs);

  const tests = reports.flatMap((report) =>
    (report.results?.tests ?? []).map((test) => ({
      assembly: assemblyOf(report),
      name: test.name,
      status: test.status,
      durationMs: test.duration ?? 0,
    })),
  );

  return { assemblies, tests };
}

// `Ns.Class.Method(arg: "a.b")` → `Ns.Class`: the arguments go first, since they may hold dots.
// A name with no dot (a custom DisplayName) is its own class.
const classOf = (testName) => {
  const parts = String(testName).split('(')[0].split('.');
  return parts.length > 1 ? parts.slice(0, -1).join('.') : parts[0];
};

// The few numbers the CI-speed trend (#1550) reads across runs, kept far longer than the raw reports.
export function totalsOf({ tests }) {
  const classes = {};
  for (const test of tests) {
    const name = classOf(test.name);
    classes[name] = (classes[name] ?? 0) + test.durationMs;
  }
  return {
    tests: tests.length,
    totalMs: tests.reduce((sum, test) => sum + test.durationMs, 0),
    slowTests: tests.filter((test) => test.durationMs >= SLOW_TEST_MS).length,
    classes,
  };
}

export function renderMarkdown({ assemblies, tests }, { title, top }) {
  const slowest = [...tests].sort((a, b) => b.durationMs - a.durationMs).slice(0, top);
  const totalMs = tests.reduce((sum, test) => sum + test.durationMs, 0);
  const slowestMs = slowest.reduce((sum, test) => sum + test.durationMs, 0);
  const share = totalMs > 0 ? Math.round((100 * slowestMs) / totalMs) : 0;

  return [
    `### ⏱️ ${title}`,
    '',
    `${tests.length} tests, ${seconds(totalMs)} of test time. The ${slowest.length} slowest take ${share}% of it.`,
    '',
    '| Test app | Tests | Failed | Skipped | Wall time |',
    '| --- | ---: | ---: | ---: | ---: |',
    ...assemblies.map((a) => `| ${cell(a.name)} | ${a.tests} | ${a.failed} | ${a.skipped} | ${seconds(a.durationMs)} |`),
    '',
    '| Slowest tests | Status | Time |',
    '| --- | --- | ---: |',
    ...slowest.map((t) => `| ${cell(t.name)} | ${t.status} | ${seconds(t.durationMs)} |`),
    '',
  ].join('\n');
}
