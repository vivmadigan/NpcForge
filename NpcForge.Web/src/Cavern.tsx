import { useEffect, useRef } from 'react';
import { forge, paint } from './Sprite';

// The cavern behind the page: dark rock at the top that falls to a lava glow, drawn on a canvas at
// six screen pixels to a canvas pixel. It sits behind the page and never under text, which always has
// a panel's flat colour under it (ADR-017, Consequences: axe cannot judge text over a picture). Every
// colour is read from a token in tokens.css (ADR-017 decision 5). The random numbers are seeded, so
// one size draws one picture and a screenshot of a state does not change between runs.

const PX = 6;
const BAYER = [
  [0, 8, 2, 10],
  [12, 4, 14, 6],
  [3, 11, 1, 9],
  [15, 7, 13, 5],
];

type Rgb = [number, number, number];

function mulberry(seed: number) {
  let a = seed;
  return () => {
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// A canvas cannot read var(--x), so the token's value is read from the page's styles. It fails loudly
// when a token is not a six digit hex colour, rather than drawing black.
function reader(styles: CSSStyleDeclaration) {
  return (token: string): Rgb => {
    const hex = styles.getPropertyValue(token).trim();
    if (!/^#[0-9a-f]{6}$/i.test(hex)) throw new Error(`${token} must be a six digit hex colour in tokens.css, not "${hex}"`);
    return [parseInt(hex.slice(1, 3), 16), parseInt(hex.slice(3, 5), 16), parseInt(hex.slice(5, 7), 16)];
  };
}

function draw(canvas: HTMLCanvasElement, width: number, height: number) {
  const colour = reader(getComputedStyle(document.documentElement));
  const ramp = Array.from({ length: 11 }, (_, i) => colour(`--cave-ramp-${i}`));
  const rock = colour('--cave-rock');
  const rockBack = colour('--cave-rock-back');
  const rockLit = colour('--cave-rock-lit');
  const glint = colour('--cave-glint');
  const ember = colour('--px-flame-hot');
  const lavaCrest = colour('--px-flame-core');
  const lavaDeep = colour('--main-bg');
  const lavaBody = colour('--accent');

  const cols = Math.ceil(width / PX);
  const rows = Math.ceil(height / PX);
  canvas.width = cols;
  canvas.height = rows;
  canvas.style.width = `${cols * PX}px`;
  canvas.style.height = `${rows * PX}px`;
  const ctx = canvas.getContext('2d')!;
  const img = ctx.createImageData(cols, rows);
  const rnd = mulberry(11);
  const put = (x: number, y: number, c: Rgb) => {
    if (x < 0 || y < 0 || x >= cols || y >= rows) return;
    const i = (y * cols + x) * 4;
    img.data[i] = c[0];
    img.data[i + 1] = c[1];
    img.data[i + 2] = c[2];
    img.data[i + 3] = 255;
  };

  // The glow fills the last 170 rows; a longer page is dark rock above that.
  const span = Math.min(rows, 170);
  const heat = (y: number) => 10 * Math.pow(Math.max(0, (y - (rows - span)) / span), 2.2);
  for (let y = 0; y < rows; y++) {
    for (let x = 0; x < cols; x++) {
      const h = heat(y);
      const lo = Math.floor(h);
      put(x, y, ramp[Math.min(10, lo + ((h - lo) * 16 > BAYER[y & 3][x & 3] ? 1 : 0))]);
    }
  }
  // Glints: cold up high, embers near the lava.
  for (let n = Math.round(cols * rows * 0.0035); n > 0; n--) {
    const x = Math.floor(rnd() * cols);
    const y = Math.floor(rnd() * rows);
    const c = heat(y) > 3.5 ? ember : glint;
    put(x, y, c);
    if (rnd() < 0.12) {
      put(x - 1, y, c);
      put(x + 1, y, c);
      put(x, y - 1, c);
      put(x, y + 1, c);
    }
  }

  // Stalactites: long at the sides, short over the title.
  const spikes = (count: number, minL: number, maxL: number, minW: number, maxW: number) => {
    const a = new Float32Array(cols).fill(1);
    for (let i = 0; i < count; i++) {
      const cx = rnd() * cols;
      const side = 0.3 + (0.7 * Math.abs(cx - cols / 2)) / (cols / 2);
      const len = (minL + rnd() * (maxL - minL)) * side;
      const w = minW + rnd() * (maxW - minW);
      for (let x = Math.floor(cx - w); x <= cx + w; x++) if (x >= 0 && x < cols) a[x] = Math.max(a[x], len * (1 - Math.abs(x - cx) / w));
    }
    return a;
  };
  const back = spikes(cols / 7, 8, 30, 2, 5);
  const front = spikes(cols / 11, 8, 38, 3, 7);
  for (let x = 0; x < cols; x++) {
    const q = x & ~1;
    for (let y = 0; y < back[q] + 2; y++) put(x, y, rockBack);
  }
  for (let x = 0; x < cols; x++) {
    const q = x & ~1;
    for (let y = 0; y < front[q]; y++) put(x, y, rock);
  }

  // The side walls wander in and out.
  const maxWall = Math.max(2, Math.min(12, Math.floor(cols * 0.05)));
  for (const right of [false, true]) {
    let w = maxWall / 2;
    for (let y = 0; y < rows; y++) {
      if (y % 3 === 0) w = Math.max(1, Math.min(maxWall, w + Math.round(rnd() * 4 - 2)));
      for (let i = 0; i < w; i++) put(right ? cols - 1 - i : i, y, rock);
    }
  }

  // The floor: rock high at the sides, a lava pool in the middle, the forge on a ledge at the widths
  // that have room for it.
  const lavaTop = (x: number) => rows - 8 + ((x >> 2) & 1);
  const rockTop = new Int32Array(cols);
  let bump = 0;
  for (let x = 0; x < cols; x++) {
    if (x % 3 === 0) bump = Math.round(rnd() * 5);
    rockTop[x] = rows - Math.round(3 + Math.pow(Math.abs(x - cols / 2) / (cols / 2), 1.5) * 26 + bump);
  }
  const forgeX = Math.round(cols * 0.66);
  const ledge = rows - 13;
  const hasForge = cols >= 150;
  if (hasForge) for (let x = forgeX - 3; x < forgeX + forge[0].length + 3 && x < cols; x++) rockTop[x] = Math.min(rockTop[x], ledge);
  for (let x = 0; x < cols; x++) {
    const top = lavaTop(x);
    for (let y = top; y < rows; y++) {
      const deep = (y - top) / 8;
      put(x, y, y === top ? (x % 3 ? ember : lavaCrest) : deep * 16 > BAYER[y & 3][x & 3] + 4 ? lavaDeep : rnd() < 0.05 ? ember : lavaBody);
    }
    if (rockTop[x] < top) for (let y = rockTop[x]; y < rows; y++) put(x, y, y === rockTop[x] ? rockLit : rock);
  }
  if (hasForge) {
    forge.forEach((row, y) => {
      [...row].forEach((c, x) => {
        if (c !== '.') put(forgeX + x, ledge - forge.length + y, colour(`--${paint[c]}`));
      });
    });
  }
  ctx.putImageData(img, 0, 0);
}

// Drawn again whenever the page changes size, which a font loading or a new step does.
export function Cavern() {
  const canvas = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    const cv = canvas.current!;
    const cave = cv.parentElement!;
    let queued = 0;
    const watch = new ResizeObserver(() => {
      cancelAnimationFrame(queued);
      queued = requestAnimationFrame(() => draw(cv, cave.clientWidth, cave.clientHeight));
    });
    watch.observe(cave);
    return () => {
      watch.disconnect();
      cancelAnimationFrame(queued);
    };
  }, []);
  return <canvas ref={canvas} className="cavern" aria-hidden="true" />;
}
