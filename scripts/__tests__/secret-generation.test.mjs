import assert from 'node:assert/strict';
import test from 'node:test';
import {
  randomBase64,
  randomHex,
  randomSigningKey,
} from '../lib/secret-generation.mjs';

test('randomHex returns a hex string with expected length', () => {
  const value = randomHex(32);
  assert.equal(value.length, 64);
  assert.match(value, /^[a-f0-9]+$/);
});

test('randomBase64 returns a non-empty base64-looking string', () => {
  const value = randomBase64(32);
  assert.ok(value.length > 0);
  assert.match(value, /^[A-Za-z0-9+/=]+$/);
});

test('randomSigningKey returns exact length and allowed chars', () => {
  const value = randomSigningKey(64);
  assert.equal(value.length, 64);
  assert.match(
    value,
    /^[A-Za-z0-9\-_!@#$%^&*+=]+$/,
  );
});

