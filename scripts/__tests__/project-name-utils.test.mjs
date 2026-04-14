import assert from 'node:assert/strict';
import test from 'node:test';

import { normalizeProjectName } from '../lib/project-name-utils.mjs';

test('normalizeProjectName returns fallback for empty input', () => {
  assert.equal(normalizeProjectName(''), 'endatix');
  assert.equal(normalizeProjectName('   '), 'endatix');
  assert.equal(normalizeProjectName(null), 'endatix');
  assert.equal(normalizeProjectName('---___***---'), 'endatix');
});

test('normalizeProjectName lowercases and normalizes separators', () => {
  assert.equal(normalizeProjectName('  My_Project Name  '), 'my-project-name');
  assert.equal(normalizeProjectName('a---b___c'), 'a-b-c');
});

test('normalizeProjectName trims leading and trailing dashes', () => {
  assert.equal(normalizeProjectName('-endatix-'), 'endatix');
  assert.equal(normalizeProjectName('---endatix---'), 'endatix');
});

test('normalizeProjectName truncates input before normalization', () => {
  const value = normalizeProjectName('Project Name With Very Very Long Identifier 1234567890');
  assert.equal(value, 'project-name-with-very-v');
});

test('normalizeProjectName coerces non-string inputs', () => {
  assert.equal(normalizeProjectName(12345), '12345');
  assert.equal(normalizeProjectName(true), 'true');
});
