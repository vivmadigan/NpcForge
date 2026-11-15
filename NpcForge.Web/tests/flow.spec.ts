import { readFileSync } from 'node:fs';
import { expect, test } from '@playwright/test';
import { bakeryCharacters } from '../src/madeUp/bakery';

// The run the made-up data is copied from. A Windows checkout may hold it with CRLF line ends.
const runFile = readFileSync(
  new URL('../../docs/runs/raw/2026-11-08-01-epic4-bakery-roll-again.md', import.meta.url),
  'utf8',
).replace(/\r\n/g, '\n');

test('each made-up sheet is the bakery run file, byte for byte', () => {
  expect(bakeryCharacters).toHaveLength(3);
  for (const { name, sheet } of bakeryCharacters) {
    // Whole lines, from the sheet's first heading to the line before the console's keep question.
    expect(runFile.includes(`\n${sheet}\nKeep this character?`), `${name}'s sheet is in the run file`).toBe(true);
  }
});
