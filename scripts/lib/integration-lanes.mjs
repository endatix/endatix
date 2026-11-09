// Checks that integration-test lanes split a filter's tests: every test in exactly one lane.
// Used by scripts/check-integration-lanes.mjs.

/** Test names from `dotnet test --list-tests` output: the lines indented by two spaces. */
export function parseTestList(output) {
  return output
    .split(/\r?\n/)
    .filter((line) => line.startsWith('  ') && line.trim() !== '')
    .map((line) => line.trim());
}

function countNames(names) {
  const counts = new Map();
  for (const name of names) counts.set(name, (counts.get(name) ?? 0) + 1);
  return counts;
}

/**
 * Compares the unsplit list with the lanes' lists as multisets (theory rows can share a name).
 * `missing`: tests the unsplit filter runs and no lane does. `extra`: tests more than one lane
 * runs, or a lane runs and the unsplit filter does not.
 */
export function compareLanes(all, lanes) {
  const expected = countNames(all);
  const actual = countNames(lanes.flat());
  const missing = [];
  const extra = [];
  for (const name of new Set([...expected.keys(), ...actual.keys()])) {
    const diff = (expected.get(name) ?? 0) - (actual.get(name) ?? 0);
    for (let i = 0; i < Math.abs(diff); i++) (diff > 0 ? missing : extra).push(name);
  }
  return { missing: missing.sort(), extra: extra.sort() };
}
