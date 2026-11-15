import type { ReactNode } from 'react';
import Markdown from 'react-markdown';
import type { BriefEntry } from './api';

// The sheet goes to Markdown whole, with no plugins, components or options, and its own section
// holds nothing else. The page never splits, searches or trims the text (AGENTS.md, "Never parse the
// model's prose"; ADR-017 decision 4). The name comes from `name`, never from the text.
//
// `actions`, on the forge page, sit at the top of the side column above the brief: under the sheet
// on a phone, and beside the sheet's first lines at desktop width. They do not follow the scroll.
// A side column taller than the window sticks for half the sheet only, and a brief that scrolls
// inside it fails the contrast check, which cannot judge text a scroll box clips (lesson 019).
export function Sheet({
  name,
  brief,
  sheet,
  actions,
}: {
  name: string | null;
  brief: BriefEntry[];
  sheet: string;
  actions?: ReactNode;
}) {
  return (
    <article className="character">
      <div className="sheet">
        <h1 className="character-name">{name ?? '(no name)'}</h1>
        <section aria-label="Character sheet">
          <Markdown>{sheet}</Markdown>
        </section>
      </div>
      <div className="side">
        {actions}
        <aside className="brief" aria-label="Brief">
          <h2>BRIEF</h2>
          <dl>
            {brief.map(({ field, value }) => (
              <div key={field}>
                <dt>{field}</dt>
                <dd>{value ?? '[not given]'}</dd>
              </div>
            ))}
          </dl>
        </aside>
      </div>
    </article>
  );
}
