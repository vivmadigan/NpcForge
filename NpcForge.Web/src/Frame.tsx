import { useEffect, useState, type ReactNode } from 'react';
import { Cavern } from './Cavern';
import { ApiError, listSaves } from './api';
import { anvil, Sprite } from './Sprite';

// The header, then the cavern with the page on it. `here` marks the current page in the header;
// `wide` is for the character page's top panel, which is wider than the parchment. Pure:
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
// (ADR-017 decision 6). Null until it arrives. `recount` asks again, after the forge page saves. When
// the API faults the header shows no count, as it does before one arrives; the page under it shows
// the failure itself.
export function useSavedCount(): { count: number | null; recount: () => void } {
  const [count, setCount] = useState<number | null>(null);
  const recount = () => {
    listSaves().then(
      (saves) => setCount(saves.length),
      (fault) => {
        if (!(fault instanceof ApiError)) throw fault;
        setCount(null);
      },
    );
  };
  useEffect(recount, []);
  return { count, recount };
}
