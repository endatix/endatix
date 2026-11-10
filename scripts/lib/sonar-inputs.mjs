// Decides whether the SonarCloud end job can analyze this run. Used by scripts/check-sonar-inputs.mjs.

const FINISHED = new Set(['success', 'failure', 'timed_out']);

/**
 * @param {object} run
 * @param {string[]} run.required  artifact names `end` reads: every lane's coverage and the scanner state
 * @param {string[]} run.present   names of the run's artifacts that have not expired
 * @param {number} run.attempt     this attempt's number
 * @param {{ name: string, run_attempt: number, conclusion: string | null }[]} run.jobs  the run's jobs, every attempt
 * @param {string} run.sonarJob    the end job's name
 * @returns {{ action: 'analyze' } | { action: 'skip', greenAttempt: number, missing: string[] } | { action: 'fail', missing: string[] }}
 *   `analyze` when every input is there. `skip` when one is missing and the latest earlier attempt's
 *   SonarCloud job passed for this commit (its cleanup deleted them). `fail` otherwise: never analyze partial coverage.
 */
export function decideSonarRun({ required, present, attempt, jobs, sonarJob }) {
  const have = new Set(present);
  const missing = required.filter((name) => !have.has(name));
  if (missing.length === 0) return { action: 'analyze' };
  // Only the latest finished analysis counts: a red one after a green one must stay red.
  // A skipped or cancelled attempt analyzed nothing and deleted nothing, so it is passed over.
  const latest = jobs
    .filter((job) => job.name === sonarJob && job.run_attempt < attempt && FINISHED.has(job.conclusion))
    .sort((a, b) => b.run_attempt - a.run_attempt)[0];
  if (latest?.conclusion === 'success') return { action: 'skip', greenAttempt: latest.run_attempt, missing };
  return { action: 'fail', missing };
}
