import { useEffect, useState, type ReactNode } from 'react';
import { Cavern } from './Cavern';
import { listSaves } from './api';
import { anvil, Sprite } from './Sprite';

// The header, then the cavern with the page on it. `here` marks the current page in the header;
// `wide` is for a character beside its side column, on the forge page and on an opened save. Pure:
// the page hands it the count, and null shows none. The header is flat and outside the cavern, and
// every panel inside the page has its own flat colour, so no text sits on the picture (ADR-017,
// Consequences).
export function Frame({
  saved,
  here,
  wide = false,
  children,
}: {
  saved: number | null;
  here?: 'forge' | 'saved';
  wide?: boolean;
  children: ReactNode;
}) {
  return (
    <>
      <header className="top">
        <a className="logo" href="/">
          <Sprite art={anvil} scale={3} />
          NPC FORGE
        </a>
        <nav aria-label="Pages">
          <a href="/" aria-current={here === 'forge' ? 'page' : undefined}>
            FORGE
          </a>
          <a href="/saved" aria-current={here === 'saved' ? 'page' : undefined}>
            SAVED CHARACTERS{saved === null ? '' : ' '}
            {saved !== null && <span className="count">{saved}</span>}
          </a>
        </nav>
      </header>
      <div className="cave">
        <Cavern />
        <main className={wide ? 'screen wide' : 'screen'}>{children}</main>
      </div>
    </>
  );
}

// The header's count. A seam call of its own, so a page opened on a named state still shows it
// (ADR-017 decision 6). Null until it arrives. `recount` asks again, after the forge page saves.
export function useSavedCount(): { count: number | null; recount: () => void } {
  const [count, setCount] = useState<number | null>(null);
  const recount = () => {
    listSaves().then((saves) => setCount(saves.length));
  };
  useEffect(recount, []);
  return { count, recount };
}
