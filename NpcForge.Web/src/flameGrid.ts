// The flame's grid: the old "Doom fire", a grid of heat that climbs one row a frame.
// Pure, with no React and no DOM, so a test can step it with a seeded random source and look at every
// cell (ADR-018 decision 8). Flame.tsx draws it. Not named flame.ts, which Flame.tsx would shadow on a
// case-insensitive disk.

export const WIDTH = 64;
export const HEIGHT = 64;
// The hottest a cell can be. Flame.tsx maps 1 to LEVELS onto the flame's colours.
export const LEVELS = 16;
// The canvas is wider and taller than the flame. A spark this near an edge cools twice as fast.
const MARGIN = 6;

// One byte of heat a cell, row by row from the top. 0 is dark.
export type Flame = Uint8Array;

// A bed of fuel on the bottom row, hottest in the middle.
export function newFlame(): Flame {
  const fire = new Uint8Array(WIDTH * HEIGHT);
  for (let x = 0; x < WIDTH; x++) {
    fire[(HEIGHT - 1) * WIDTH + x] = Math.max(0, Math.round(LEVELS - Math.abs(x - (WIDTH - 1) / 2) * 1.2));
  }
  return fire;
}

// Steps the flame one frame, in place. `random` gives numbers from 0 up to but not including 1, so a
// test passes a seeded one and the page passes Math.random.
export function burn(fire: Flame, random: () => number): void {
  // Rows from the top down, so a spark climbs one row a frame. When two sparks land on one cell the
  // later one wins, so each row is walked left or right at random and the flame leans neither way.
  for (let y = 1; y < HEIGHT; y++) {
    const leftward = random() < 0.5;
    for (let i = 0; i < WIDTH; i++) {
      const x = leftward ? WIDTH - 1 - i : i;
      const target = x + 1 - Math.floor(random() * 3);
      // The outermost cells are never lit, so the flame dies inside its box and never meets an edge.
      if (y - 1 === 0 || target <= 0 || target >= WIDTH - 1) continue;
      const inMargin = y - 1 < MARGIN || target < MARGIN || target >= WIDTH - MARGIN;
      const cool = (random() < 0.36 ? 1 : 0) + (inMargin ? 1 : 0);
      fire[(y - 1) * WIDTH + target] = Math.max(0, fire[y * WIDTH + x] - cool);
    }
  }
}

// A small seeded source (mulberry32), so the still flame is the same picture on every run.
export function seeded(seed: number): () => number {
  let a = seed;
  return () => {
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// The flame as it first appears, and as it holds for someone who asks for less motion: 120 frames of
// a fixed seed, so a screenshot of the wait is the same on every run.
export function stillFlame(): Flame {
  const fire = newFlame();
  const random = seeded(7);
  for (let frame = 0; frame < 120; frame++) burn(fire, random);
  return fire;
}
