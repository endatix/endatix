import assert from 'node:assert/strict';
import test from 'node:test';
import {
  tipText,
  printGeneratedFiles,
  printNextSteps,
  warningText,
} from '../lib/console-format.mjs';

test('warningText includes warning symbol and text', () => {
  const message = warningText('example warning');
  assert.match(message, /example warning/);
  assert.match(message, /⚠/);
});

test('tipText includes lightbulb and message', () => {
  const message = tipText('example tip');
  assert.match(message, /example tip/);
  assert.match(message, /💡/);
  assert.match(message, /\x1b\[90m/);
});

test('print helpers emit expected headings', () => {
  const originalLog = console.log;
  const output = [];

  console.log = (...args) => {
    output.push(args.join(' '));
  };

  try {
    printGeneratedFiles(['/tmp/one', '/tmp/two']);
    printNextSteps(['  1) one', '  2) two']);
  } finally {
    console.log = originalLog;
  }

  assert.ok(
    output.some((line) => line.includes('Endatix Azure quickstart secrets generated')),
  );
  assert.ok(output.some((line) => line.includes('Next steps')));
  assert.ok(output.some((line) => line.includes('/tmp/one')));
});

