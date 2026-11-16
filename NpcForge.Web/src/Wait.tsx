import { useEffect, useRef } from 'react';
import { Flame } from './Flame';

// The sketch's words for the two waits (docs/design/cavern-sketch.html).
const words = {
  reading: { title: 'READING YOUR SCENE', line: 'Working out what your scene already says.' },
  writing: {
    title: 'AT THE FORGE',
    line: 'The dice settle who they are, a name follows, and the character is written. This takes a little while.',
  },
};

// The wait: a black screen over the whole window, a flame, a title in the pixel face and one dim
// line. It is on screen exactly as long as the page is in a wait state, which is as long as the seam
// takes (ADR-018 decision 8). `fade` is true when there is a step to fade out from; a state opened by
// name has none and shows black at once. The text sits on the screen's own flat black, so the contrast
// check can judge it (lesson 019).
export function Wait({ kind, fade }: { kind: keyof typeof words; fade: boolean }) {
  const { title, line } = words[kind];
  // The button that started the wait is inert under it, so focus moves here and a keyboard or
  // screen reader is on the wait, not on nothing.
  const wait = useRef<HTMLDivElement>(null);
  useEffect(() => wait.current?.focus({ preventScroll: true }), []);
  return (
    <div className={fade ? 'wait wait-fade' : 'wait'} role="status" tabIndex={-1} ref={wait}>
      <Flame />
      <h1 className="wait-title">{title}</h1>
      <p>{line}</p>
    </div>
  );
}
