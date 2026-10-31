// Our own reading of the SonarCloud quality gate. SonarCloud Free cannot edit "Sonar way",
// so CI asks for the gate's result and decides which red conditions fail the job.

/**
 * Splits a gate's red conditions into blocking and advisory ones.
 * @param {{ status: string, conditions?: Array<{ metricKey: string, status: string }> }} projectStatus
 * @param {string[]} advisoryMetrics metric keys that warn instead of failing; '*' makes every one warn
 */
export function evaluateGate(projectStatus, advisoryMetrics) {
  const red = (projectStatus.conditions ?? []).filter((c) => c.status === 'ERROR');
  const warnsOnly = (c) => advisoryMetrics.includes('*') || advisoryMetrics.includes(c.metricKey);
  const advisory = red.filter(warnsOnly);
  const blocking = red.filter((c) => !warnsOnly(c));
  // A red gate with no red condition is a state we do not understand: fail rather than wave it through.
  const unexplained = projectStatus.status !== 'OK' && red.length === 0;
  return { passed: blocking.length === 0 && !unexplained, blocking, advisory };
}

/** Reads the key=value report-task.txt the scanner writes after it uploads an analysis. */
export function parseReportTask(text) {
  const entries = text
    .split(/\r?\n/)
    .map((line) => line.match(/^([^=]+)=(.*)$/))
    .filter(Boolean)
    .map(([, key, value]) => [key.trim(), value.trim()]);
  return Object.fromEntries(entries);
}

/** One line per condition, e.g. "new_duplicated_lines_density is 4.7 (limit GT 3)". */
export function describeCondition(condition) {
  return `${condition.metricKey} is ${condition.actualValue} (limit ${condition.comparator} ${condition.errorThreshold})`;
}

/**
 * A SonarCloud Web API URL on a fixed host, e.g. sonarApiUrl(host, '/api/ce/task', { id }).
 * A task id or analysis id from report-task.txt only ever lands in the query string.
 */
export function sonarApiUrl(host, path, params) {
  const url = new URL(path, host);
  for (const [key, value] of Object.entries(params)) {
    if (!value) throw new Error(`report-task.txt gave no ${key} for ${path}`);
    url.searchParams.set(key, value);
  }
  return url.toString();
}
