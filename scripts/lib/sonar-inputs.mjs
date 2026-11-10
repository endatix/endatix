// Decides whether the SonarCloud end job can analyze this run. Used by scripts/check-sonar-inputs.mjs.

/**
 * @param {object} run
 * @param {string[]} run.required  artifact names `end` reads: every lane's coverage and the scanner state
 * @param {string[]} run.present   names of the run's artifacts that have not expired
 * @param {number} run.attempt     this attempt's number
 * @param {{ name: string, run_attempt: number, conclusion: string | null }[]} run.jobs  the run's jobs, every attempt
 * @param {string} run.sonarJob    the end job's name
 * @returns {{ action: 'analyze' } | { action: 'skip', greenAttempt: number, missing: string[] } | { action: 'fail', missing: string[] }}
 *   `analyze` when every input is there. `skip` when one is missing and an earlier attempt's analysis
 *   of this commit already passed (its cleanup deleted them). `fail` otherwise: never analyze partial coverage.
 */
export function decideSonarRun({ required, present, attempt, jobs, sonarJob }) {
  const have = new Set(present);
  const missing = required.filter((name) => !have.has(name));
  if (missing.length === 0) return { action: 'analyze' };
  const green = jobs
    .filter((job) => job.name === sonarJob && job.run_attempt < attempt && job.conclusion === 'success')
    .map((job) => job.run_attempt);
  if (green.length > 0) return { action: 'skip', greenAttempt: Math.max(...green), missing };
  return { action: 'fail', missing };
}
