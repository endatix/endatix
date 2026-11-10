import assert from 'node:assert/strict';
import { test } from 'node:test';
import { compareLanes, parseTestList } from '../lib/integration-lanes.mjs';

test('parseTestList keeps only the indented test names', () => {
  const output = [
    'Discovering tests from /x/Endatix.IntegrationTests.dll (net10.0|x64)',
    '',
    'Discovered 2 tests in assembly - /x/Endatix.IntegrationTests.dll (net10.0|x64)',
    '  A.B.First',
    '  A.B.Second(write: "PUT")\r',
    '',
    'Discovered 2 tests.',
  ].join('\n');

  assert.deepEqual(parseTestList(output), ['A.B.First', 'A.B.Second(write: "PUT")']);
});

test('lanes that split the tests pass', () => {
  assert.deepEqual(compareLanes(['a', 'b', 'c'], [['a'], ['c', 'b']]), { missing: [], extra: [] });
});

test('a test in no lane is missing', () => {
  assert.deepEqual(compareLanes(['a', 'b'], [['a'], []]), { missing: ['b'], extra: [] });
});

test('a test in both lanes is extra', () => {
  assert.deepEqual(compareLanes(['a', 'b'], [['a', 'b'], ['b']]), { missing: [], extra: ['b'] });
});

test('theory rows with one name are counted, not merged', () => {
  assert.deepEqual(compareLanes(['t', 't'], [['t'], []]), { missing: ['t'], extra: [] });
});
