import { expect, test } from '@playwright/test';
import { burn, HEIGHT, LEVELS, newFlame, seeded, stillFlame, WIDTH, type Flame } from '../src/flameGrid';

// ADR-018 decision 8: the flame is a pure module, so a test steps it with a seeded random source and
// reads every cell. The edge rule: the flame dies inside its box and never meets an edge.
function litOnEdge(fire: Flame): string[] {
  const found: string[] = [];
  for (let x = 0; x < WIDTH; x++) if (fire[x] !== 0) found.push(`top (${x}, 0)`);
  for (let y = 0; y < HEIGHT; y++) {
    if (fire[y * WIDTH] !== 0) found.push(`left (0, ${y})`);
    if (fire[y * WIDTH + WIDTH - 1] !== 0) found.push(`right (${WIDTH - 1}, ${y})`);
  }
  return found;
}

// The real flame never climbs as far as an edge, so on its own it would pass with the rule removed.
// A grid lit everywhere inside its edges is the worst case the rule has to hold against.
function litEverywhereButTheEdges(): Flame {
  const fire = new Uint8Array(WIDTH * HEIGHT);
  for (let y = 1; y < HEIGHT; y++) for (let x = 1; x < WIDTH - 1; x++) fire[y * WIDTH + x] = LEVELS;
  return fire;
}

const starts: [string, () => Flame][] = [
  ['the fuel bed', newFlame],
  ['a grid lit everywhere inside its edges', litEverywhereButTheEdges],
];

for (const [start, begin] of starts) {
  test(`steps the flame 1,000 frames and finds no lit cell on the top, left or right edge of its box, from ${start}`, () => {
    const fire = begin();
    const random = seeded(1);
    let mostLit = 0;
    for (let frame = 1; frame <= 1000; frame++) {
      burn(fire, random);
      expect(litOnEdge(fire), `lit edge cells after frame ${frame}`).toEqual([]);
      mostLit = Math.max(mostLit, fire.filter((heat) => heat > 0).length);
    }
    // A grid that went dark would pass the edge rule for the wrong reason.
    expect(mostLit).toBeGreaterThan(300);
  });
}

test('the still flame is the same picture on every call, and it is burning', () => {
  const first = stillFlame();
  expect(Array.from(stillFlame())).toEqual(Array.from(first));
  expect(first.filter((heat) => heat > 0).length).toBeGreaterThan(300);
  expect(litOnEdge(first)).toEqual([]);
});
