import { useEffect, useState } from 'react';
import { listSaves, loadSave, type ListedCharacter, type SavedCharacter } from './api';
import { Frame } from './Frame';
import { Sheet } from './Sheet';

// The page holds one of these and draws from it with one switch (principle-model-the-domain). Each
// is plain data, so a state can be opened by name (ADR-017 decision 6).
export type SavedState =
  | { kind: 'loading' } // still text, nothing moves
  | { kind: 'list'; saves: ListedCharacter[] } // empty or long, same case
  | { kind: 'open'; character: SavedCharacter }
  | { kind: 'missing'; number: number }; // no save under this number

// `number` is the one in the path, or null for the list. `saved` is the bar's count. `start` opens
// a named state without calling the seam.
export function SavedPage({
  number,
  saved,
  start,
}: {
  number: number | null;
  saved: number | null;
  start?: SavedState;
}) {
  const [state, setState] = useState<SavedState>(start ?? { kind: 'loading' });

  useEffect(() => {
    if (start) return;
    if (number === null) {
      listSaves().then((saves) => setState({ kind: 'list', saves }));
    } else {
      loadSave(number).then((character) =>
        setState(character ? { kind: 'open', character } : { kind: 'missing', number }),
      );
    }
  }, [number, start]);

  switch (state.kind) {
    case 'loading':
      return (
        <Frame saved={saved}>
          <p>Reading the saves.</p>
        </Frame>
      );
    case 'list':
      // The list's own length, not the count handed in, so an empty list shows SAVED: 0.
      return (
        <Frame saved={state.saves.length}>
          <h1 className="page-title">SAVED CHARACTERS</h1>
          {state.saves.length === 0 ? (
            <>
              <p>Nothing saved yet.</p>
              <a className="button" href="/">
                FORGE A CHARACTER
              </a>
            </>
          ) : (
            <ul className="saved-list">
              {state.saves.map(({ number, name, occupation, setting }) => (
                <li key={number}>
                  <a className="row" href={`/saved/${number}`}>
                    <span className="row-number">{number}</span>
                    <span className="row-name">{name ?? '(no name)'}</span>
                    <span className="row-detail">
                      {occupation ?? '[not given]'}, {setting ?? '[not given]'}
                    </span>
                  </a>
                </li>
              ))}
            </ul>
          )}
        </Frame>
      );
    case 'open':
      return (
        <Frame saved={saved}>
          <a className="back" href="/saved">
            BACK TO SAVED
          </a>
          <Sheet name={state.character.name} brief={state.character.brief} sheet={state.character.sheet} />
        </Frame>
      );
    case 'missing':
      return (
        <Frame saved={saved}>
          <p>No save numbered {state.number}.</p>
          <a className="back" href="/saved">
            BACK TO SAVED
          </a>
        </Frame>
      );
  }
}
