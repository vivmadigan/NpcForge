import { Fragment } from 'react';
import type { ConfirmEntry } from './api';
import { sentences } from './sentences';

// ADR-018 decision 2: the sentences, drawn from their parts. A value the GM gave takes the second
// colour, and no string is searched for it. The region has a name so a test can read the sentences
// apart from the scene above them.
export function Confirmation({ entries }: { entries: ConfirmEntry[] }) {
  return (
    <section aria-label="Who is being made">
      <p className="outline">
        {sentences(entries).map((sentence, at) => (
          <Fragment key={at}>
            {at > 0 && ' '}
            {sentence.map((part, inSentence) =>
              part.kind === 'value' ? (
                <span key={inSentence} className="said">
                  {part.text}
                </span>
              ) : (
                part.text
              ),
            )}
          </Fragment>
        ))}
      </p>
    </section>
  );
}
