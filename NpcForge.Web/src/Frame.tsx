import type { ReactNode } from 'react';

// Pure: the count comes from the page's own state, and null shows none (ADR-017 decision 6).
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
