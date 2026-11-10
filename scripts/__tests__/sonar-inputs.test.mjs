import assert from 'node:assert/strict';
import test from 'node:test';
import { decideSonarRun } from '../lib/sonar-inputs.mjs';

const SONAR = 'Endatix API: SonarCloud';
const REQUIRED = [
  'coverage-unit',
  'coverage-integration-jobs',
  'coverage-integration-rest',
  'coverage-integration-sqlserver-jobs',
  'coverage-integration-sqlserver-rest',
  'sonar-scanner-state',
];
const job = (run_attempt, conclusion, name = SONAR) => ({ name, run_attempt, conclusion });

test('every input present: analyze, even on a later attempt after a green one (a full re-run)', () => {
  const decision = decideSonarRun({ required: REQUIRED, present: [...REQUIRED, 'test-totals-unit'], attempt: 2, jobs: [job(1, 'success')], sonarJob: SONAR });
  assert.deepEqual(decision, { action: 'analyze' });
});

// endatix#1666, run 42893460070 attempt 3: only the re-run unit job uploaded coverage again.
test('a one-job re-run after a green analysis skips and names what its cleanup deleted', () => {
  const decision = decideSonarRun({
    required: REQUIRED,
    present: ['coverage-unit', 'test-totals-unit'],
    attempt: 3,
    jobs: [job(1, 'success'), job(2, 'success'), job(3, null), job(3, 'success', 'Endatix API: Build & Test')],
    sonarJob: SONAR,
  });
  assert.equal(decision.action, 'skip');
  assert.equal(decision.greenAttempt, 2);
  assert.deepEqual(decision.missing, REQUIRED.slice(1));
});

test('missing lanes with no green analysis before fail, naming each missing artifact', () => {
  const decision = decideSonarRun({
    required: REQUIRED,
    present: ['coverage-unit', 'coverage-integration-rest', 'sonar-scanner-state'],
    attempt: 2,
    jobs: [job(1, 'failure')],
    sonarJob: SONAR,
  });
  assert.deepEqual(decision, {
    action: 'fail',
    missing: ['coverage-integration-jobs', 'coverage-integration-sqlserver-jobs', 'coverage-integration-sqlserver-rest'],
  });
});

test('a first attempt with a missing input fails: nothing earlier can vouch for it', () => {
  const decision = decideSonarRun({ required: REQUIRED, present: REQUIRED.slice(0, 5), attempt: 1, jobs: [job(1, null)], sonarJob: SONAR });
  assert.deepEqual(decision, { action: 'fail', missing: ['sonar-scanner-state'] });
});

test('a green job of another name does not count as a green analysis', () => {
  const decision = decideSonarRun({
    required: REQUIRED,
    present: [],
    attempt: 2,
    jobs: [job(1, 'success', 'Endatix API: SonarCloud build'), job(1, 'success', 'Endatix API: Delete consumed coverage')],
    sonarJob: SONAR,
  });
  assert.equal(decision.action, 'fail');
});
