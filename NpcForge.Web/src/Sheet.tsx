import Markdown from 'react-markdown';
import type { BriefEntry } from './api';

// The sheet goes to Markdown whole, with no plugins, components or options, and its own section
// holds nothing else. The page never splits, searches or trims the text (AGENTS.md, "Never parse the
// model's prose"; ADR-017 decision 4). The name comes from `name`, never from the text.
export function Sheet({ name, brief, sheet }: { name: string | null; brief: BriefEntry[]; sheet: string }) {
  return (
    <article className="character">
      <div className="sheet">
        <h1 className="character-name">{name ?? '(no name)'}</h1>
        <section aria-label="Character sheet">
          <Markdown>{sheet}</Markdown>
        </section>
      </div>
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
    </article>
  );
}
