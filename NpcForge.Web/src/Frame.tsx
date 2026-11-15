import { useEffect, useState, type ReactNode } from 'react';
import { listSaves } from './api';
import { anvil, Sprite } from './Sprite';

// Pure: the page hands it the count, and null shows none.
export function Frame({ saved, children }: { saved: number | null; children: ReactNode }) {
  return (
    <div className="frame">
      <Wall />
      <nav className="bar">
        <a className="bar-home" href="/">
          <Sprite art={anvil} scale={3} />
          NPCFORGE
        </a>
        <a className="bar-saved" href="/saved">
          SAVED{saved === null ? '' : `: ${saved}`}
        </a>
      </nav>
      {children}
    </div>
  );
}

// The dungeon wall behind the frame: dark stone blocks with hard edges, drawn in code. It sits behind
// the page and never under text, which always has the frame's flat colour under it (ADR-017,
// Consequences: axe cannot judge text over a pattern).
function Wall() {
  return (
    <svg className="wall" aria-hidden="true">
      <defs>
        <pattern id="wall-blocks" width="48" height="24" patternUnits="userSpaceOnUse">
          <rect className="wall-stone" width="48" height="24" />
          <rect className="wall-stone-lit" width="48" height="2" />
          <rect className="wall-stone-lit" y="12" width="48" height="2" />
          <rect className="wall-mortar" y="10" width="48" height="2" />
          <rect className="wall-mortar" y="22" width="48" height="2" />
          <rect className="wall-mortar" x="46" width="2" height="12" />
          <rect className="wall-mortar" x="22" y="12" width="2" height="12" />
        </pattern>
      </defs>
      <rect width="100%" height="100%" fill="url(#wall-blocks)" />
    </svg>
  );
}

// The bar's count. A seam call of its own, so a page opened on a named state still shows it
// (ADR-017 decision 6). Null until it arrives. `recount` asks again, after the forge page saves.
export function useSavedCount(): { count: number | null; recount: () => void } {
  const [count, setCount] = useState<number | null>(null);
  const recount = () => {
    listSaves().then((saves) => setCount(saves.length));
  };
  useEffect(recount, []);
  return { count, recount };
}
