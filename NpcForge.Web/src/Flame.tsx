import { useEffect, useRef } from 'react';
import { burn, HEIGHT, LEVELS, stillFlame, WIDTH, type Flame as Grid } from './flameGrid';

// The flame's colours from coolest to hottest, as the tokens that hold them (ADR-017 decision 5). A
// canvas cannot read var(--x), so each is read from the page's styles; fillStyle takes the value as
// written.
const tokens = [
  '--frame-shadow',
  '--flame-smoulder',
  '--px-glow',
  '--molten-deep',
  '--accent',
  '--px-flame-hot',
  '--px-flame-core',
];

// Twelve frames a second, as the sketch's.
const frameMs = 1000 / 12;

function paint(ctx: CanvasRenderingContext2D, fire: Grid, colours: string[]) {
  ctx.clearRect(0, 0, WIDTH, HEIGHT);
  for (let i = 0; i < fire.length; i++) {
    if (fire[i] === 0) continue;
    ctx.fillStyle = colours[Math.ceil((fire[i] / LEVELS) * colours.length) - 1];
    ctx.fillRect(i % WIDTH, Math.floor(i / WIDTH), 1, 1);
  }
}

// Drawn from the fixed-seed still frame the moment it appears. It steps only while the device allows
// motion, and holds that frame when the device asks for less (the epic's constraint). Hidden from
// assistive technology: the words beside it say what is happening.
export function Flame() {
  const canvas = useRef<HTMLCanvasElement>(null);
  useEffect(() => {
    const ctx = canvas.current!.getContext('2d')!;
    const styles = getComputedStyle(document.documentElement);
    const colours = tokens.map((token) => styles.getPropertyValue(token).trim());
    const fire = stillFlame();
    paint(ctx, fire, colours);

    const lessMotion = matchMedia('(prefers-reduced-motion: reduce)');
    let timer = 0;
    const sync = () => {
      clearInterval(timer);
      if (lessMotion.matches) return;
      timer = window.setInterval(() => {
        burn(fire, Math.random);
        paint(ctx, fire, colours);
      }, frameMs);
    };
    sync();
    lessMotion.addEventListener('change', sync);
    return () => {
      clearInterval(timer);
      lessMotion.removeEventListener('change', sync);
    };
  }, []);
  return <canvas ref={canvas} className="flame" width={WIDTH} height={HEIGHT} aria-hidden="true" />;
}
