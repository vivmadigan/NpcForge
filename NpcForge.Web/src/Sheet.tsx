import Markdown from 'react-markdown';

// The sheet goes to Markdown whole, with no plugins, components or options, and its own section
// holds nothing else. The page never splits, searches or trims the text (AGENTS.md, "Never parse the
// model's prose"; ADR-017 decision 4). The name comes from `name`, never from the text. The parchment
// stands alone, centred: no page shows the brief beside it (ADR-018 decision 9), and save, roll again
// and leave are the forge page's own panels above and below it.
export function Sheet({ name, sheet }: { name: string | null; sheet: string }) {
  return (
    <article className="sheet">
      <h1 className="character-name">{name ?? '(no name)'}</h1>
      <section aria-label="Character sheet">
        <Markdown>{sheet}</Markdown>
      </section>
    </article>
  );
}
