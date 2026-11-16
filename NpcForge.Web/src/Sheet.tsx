import type { ReactNode } from 'react';
import Markdown from 'react-markdown';

// The sheet goes to Markdown whole, with no plugins, components or options, and its own section
// holds nothing else. The page never splits, searches or trims the text (AGENTS.md, "Never parse the
// model's prose"; ADR-017 decision 4). The name comes from `name`, never from the text. No page shows
// the brief beside it (ADR-018 decision 9).
//
// `actions`, on the forge page, sit in a column beside the sheet at desktop width and under it on a
// phone. They do not follow the scroll.
export function Sheet({ name, sheet, actions }: { name: string | null; sheet: string; actions?: ReactNode }) {
  return (
    <article className="character">
      <div className="sheet">
        <h1 className="character-name">{name ?? '(no name)'}</h1>
        <section aria-label="Character sheet">
          <Markdown>{sheet}</Markdown>
        </section>
      </div>
      {actions && <div className="side">{actions}</div>}
    </article>
  );
}
