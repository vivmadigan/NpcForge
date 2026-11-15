import { useEffect, useState, type ReactNode } from 'react';
import { listSaves } from './api';

// Pure: the page hands it the count, and null shows none.
export function Frame({ saved, children }: { saved: number | null; children: ReactNode }) {
  return (
    <div className="frame">
      <nav className="bar">
        <a href="/">NPCFORGE</a>
        <a className="bar-saved" href="/saved">
          SAVED{saved === null ? '' : `: ${saved}`}
        </a>
      </nav>
      {children}
    </div>
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
