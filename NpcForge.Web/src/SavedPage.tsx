import { useEffect, useState, type ReactElement } from 'react';
import { ApiError, listSaves, loadSave, type ListedCharacter, type SavedCharacter } from './api';
import { Frame } from './Frame';
import { Sheet } from './Sheet';

// The page holds one of these and draws from it with one switch (principle-model-the-domain). Each
// is plain data, so a state can be opened by name (ADR-017 decision 6).
export type SavedState =
  | { kind: 'loading' } // still text, nothing moves
  | { kind: 'list'; saves: ListedCharacter[] } // empty or long, same case
  | { kind: 'open'; character: SavedCharacter }
  | { kind: 'missing'; number: number } // no save under this number
  | { kind: 'failed'; reason: string }; // the API's fault, as it sent it (ADR-019 decision 4)

// A fault from the API is the failure state. Anything else thrown is a bug, and is left to surface.
function failed(fault: unknown): SavedState {
  if (!(fault instanceof ApiError)) throw fault;
  return { kind: 'failed', reason: fault.message };
}

// `number` is the one in the path, or null for the list. `saved` is the header's count. `start` opens
// a named state without calling the seam. The return type has no `undefined` in it, so a state
// with no case in the switch fails tsc.
export function SavedPage({
  number,
  saved,
  start,
}: {
  number: number | null;
  saved: number | null;
  start?: SavedState;
}): ReactElement {
  const [state, setState] = useState<SavedState>(start ?? { kind: 'loading' });

  useEffect(() => {
    if (start) return;
    if (number === null) {
      listSaves().then(
        (saves) => setState({ kind: 'list', saves }),
        (fault) => setState(failed(fault)),
      );
    } else {
      loadSave(number).then(
        (character) => setState(character ? { kind: 'open', character } : { kind: 'missing', number }),
        (fault) => setState(failed(fault)),
      );
    }
  }, [number, start]);

  switch (state.kind) {
    case 'loading':
      return (
        <Frame saved={saved} here="saved">
          <div className="panel">
            <p>Reading the saves.</p>
          </div>
        </Frame>
      );
    case 'list':
      // The list's own length, not the count handed in, so an empty list shows a count of 0.
      return (
        <Frame saved={state.saves.length} here="saved">
          <div className="panel">
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
          </div>
        </Frame>
      );
    case 'open':
      return (
        <Frame saved={saved} here="saved">
          <a className="button back" href="/saved">
            BACK TO SAVED
          </a>
          <Sheet name={state.character.name} sheet={state.character.sheet} />
        </Frame>
      );
    case 'missing':
      return (
        <Frame saved={saved} here="saved">
          <div className="panel">
            <p>No save numbered {state.number}.</p>
            <a className="button back" href="/saved">
              BACK TO SAVED
            </a>
          </div>
        </Frame>
      );
    case 'failed':
      // The link opens this same page again, so it asks the API again.
      return (
        <Frame saved={saved} here="saved">
          <div className="panel">
            <h1 className="page-title">FAILED</h1>
            <p className="notice">The saves could not be read: {state.reason}</p>
            <a className="button" href={number === null ? '/saved' : `/saved/${number}`}>
              TRY AGAIN
            </a>
          </div>
        </Frame>
      );
  }
}
