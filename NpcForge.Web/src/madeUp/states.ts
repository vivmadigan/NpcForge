// ADR-017 decision 6: the one table of named states. A name, the path it opens on and the page's
// own state value live here and nowhere else, typed against the page's union so a state that no
// longer fits its page fails tsc. main.tsx looks a name up; tests/states.spec.ts walks the keys.
import type { SavedCharacter } from '../api';
import type { SavedState } from '../SavedPage';
import { savedCharacters, savedList } from './saves';

// One variant for each page. The forge page adds its own, `{ page: 'forge'; path: '/'; state: ForgeState }`.
export type NamedState = { page: 'saved'; path: string; state: SavedState };

function save(number: number): SavedCharacter {
  const found = savedCharacters.find((character) => character.number === number);
  if (!found) throw new Error(`The made-up saves hold no save ${number}`);
  return found;
}

export const namedStates: Record<string, NamedState> = {
  'saved-empty': { page: 'saved', path: '/saved', state: { kind: 'list', saves: [] } },
  // "A long saved list" is the 16 real saves (ADR-017, "Left to the owner").
  'saved-long': { page: 'saved', path: '/saved', state: { kind: 'list', saves: savedList } },
  'saved-open': { page: 'saved', path: '/saved/13', state: { kind: 'open', character: save(13) } },
  // The one save with a blockquote and no name, so it puts the quote band and "(no name)" under
  // the contrast check.
  'saved-open-unnamed': { page: 'saved', path: '/saved/8', state: { kind: 'open', character: save(8) } },
  'saved-missing': { page: 'saved', path: '/saved/99', state: { kind: 'missing', number: 99 } },
};
