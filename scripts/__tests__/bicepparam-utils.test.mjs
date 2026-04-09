import assert from 'node:assert/strict';
import test from 'node:test';
import {
  applyStringParamReplacements,
  readStringParamFromBicepParam,
  replaceStringParamInBicepParam,
} from '../lib/bicepparam-utils.mjs';

test('readStringParamFromBicepParam returns the param value when present', () => {
  const content = "param resource_prefix = 'eval-'\nparam environment = 'sandbox'\n";
  const value = readStringParamFromBicepParam(content, 'resource_prefix');
  assert.equal(value, 'eval-');
});

test('replaceStringParamInBicepParam replaces a single param value', () => {
  const content = "param initialUserPassword = 'CHANGE_ME'\n";
  const updated = replaceStringParamInBicepParam(
    content,
    'initialUserPassword',
    'new-secret',
  );

  assert.equal(updated, "param initialUserPassword = 'new-secret'\n");
});

test('replaceStringParamInBicepParam throws when target param is missing', () => {
  const content = "param initialUserEmail = 'admin@endatix.com'\n";
  assert.throws(
    () => replaceStringParamInBicepParam(content, 'missingParam', 'value'),
    /Required param 'missingParam' was not found/,
  );
});

test('applyStringParamReplacements applies replacements in order', () => {
  const content =
    "param initialUserPassword = 'old1'\nparam hubAuthSecret = 'old2'\n";
  const updated = applyStringParamReplacements(content, [
    ['initialUserPassword', 'new-password'],
    ['hubAuthSecret', 'new-auth-secret'],
  ]);

  assert.match(updated, /param initialUserPassword = 'new-password'/);
  assert.match(updated, /param hubAuthSecret = 'new-auth-secret'/);
});
