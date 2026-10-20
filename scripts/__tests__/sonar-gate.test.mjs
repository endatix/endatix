import assert from 'node:assert/strict';
import test from 'node:test';
import { evaluateGate, parseReportTask, sonarApiUrl } from '../lib/sonar-gate.mjs';

const ADVISORY = ['new_duplicated_lines_density'];

function condition(metricKey, status, actualValue = '1', errorThreshold = '1') {
  return { metricKey, status, actualValue, errorThreshold, comparator: 'GT' };
}

// The gate of endatix#1296: red on duplication only.
const DUPLICATION_ONLY = {
  status: 'ERROR',
  conditions: [
    condition('new_reliability_rating', 'OK'),
    condition('new_coverage', 'OK', '93.5', '80'),
    condition('new_duplicated_lines_density', 'ERROR', '4.7', '3'),
  ],
};

test('a gate red only on duplication passes with a warning', () => {
  const verdict = evaluateGate(DUPLICATION_ONLY, ADVISORY);
  assert.equal(verdict.passed, true);
  assert.deepEqual(verdict.advisory.map((c) => c.metricKey), ['new_duplicated_lines_density']);
  assert.deepEqual(verdict.blocking, []);
});

test('a red coverage condition fails the gate even next to an advisory one', () => {
  const status = {
    status: 'ERROR',
    conditions: [...DUPLICATION_ONLY.conditions, condition('new_coverage', 'ERROR', '61.0', '80')],
  };
  const verdict = evaluateGate(status, ADVISORY);
  assert.equal(verdict.passed, false);
  assert.deepEqual(verdict.blocking.map((c) => c.metricKey), ['new_coverage']);
  assert.equal(verdict.advisory.length, 1);
});

test('a green gate passes with nothing to report', () => {
  const verdict = evaluateGate({ status: 'OK', conditions: [condition('new_coverage', 'OK')] }, ADVISORY);
  assert.deepEqual(verdict, { passed: true, blocking: [], advisory: [] });
});

test('a red gate with no red condition still fails, so an unknown failure is never waved through', () => {
  const verdict = evaluateGate({ status: 'ERROR', conditions: [] }, ADVISORY);
  assert.equal(verdict.passed, false);
});

test('parseReportTask reads the key=value lines the scanner writes', () => {
  const text = [
    'organization=endatix',
    'projectKey=endatix_endatix',
    'serverUrl=https://sonarcloud.io',
    'dashboardUrl=https://sonarcloud.io/dashboard?id=endatix_endatix&pullRequest=1296',
    'ceTaskId=AZ1',
    'ceTaskUrl=https://sonarcloud.io/api/ce/task?id=AZ1',
  ].join('\n');
  const task = parseReportTask(text);
  assert.equal(task.ceTaskUrl, 'https://sonarcloud.io/api/ce/task?id=AZ1');
  assert.equal(task.dashboardUrl, 'https://sonarcloud.io/dashboard?id=endatix_endatix&pullRequest=1296');
  assert.equal(task.serverUrl, 'https://sonarcloud.io');
});

test('API URLs keep the fixed host and put report values in the query only', () => {
  assert.equal(
    sonarApiUrl('https://sonarcloud.io', '/api/ce/task', { id: 'AZ1@evil.example/x?y' }),
    'https://sonarcloud.io/api/ce/task?id=AZ1%40evil.example%2Fx%3Fy',
  );
  assert.throws(() => sonarApiUrl('https://sonarcloud.io', '/api/ce/task', { id: undefined }), /no id/);
});
