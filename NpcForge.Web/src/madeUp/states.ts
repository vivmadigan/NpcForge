// ADR-017 decision 6: the one table of named states. A name, the path it opens on and the page's
// own state value live here and nowhere else, typed against the page's union so a state that no
// longer fits its page fails tsc. main.tsx looks a name up; tests/states.spec.ts walks the keys.
import type { SavedCharacter } from '../api';
import type { ForgeState } from '../ForgePage';
import type { SavedState } from '../SavedPage';
import { bakeryCharacters, bakeryFields, bakeryScene, marketFields, marketScene } from './bakery';
import { savedCharacters, savedList } from './saves';

// One variant for each page, so main.tsx can hand each page only its own states.
export type NamedState =
  | { page: 'saved'; path: string; state: SavedState }
  | { page: 'forge'; path: '/'; state: ForgeState };

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

  'forge-describe': { page: 'forge', path: '/', state: { kind: 'describe', scene: '' } },
  'forge-reading': { page: 'forge', path: '/', state: { kind: 'reading', scene: bakeryScene } },
  // The bakery scene names a place and a role, so it is never asked. This scene leaves the role out;
  // the question is the one the console builds from the roll's own description of the field.
  'forge-question': {
    page: 'forge',
    path: '/',
    state: {
      kind: 'question',
      scene: 'The party bursts into a tiny bakery where someone is furious that the croissants keep vanishing.',
      interview: 'bakery',
      field: 'occupation',
      question: 'Who the character is, such as innkeeper or farmer',
      answer: '',
    },
  },
  'forge-confirm': {
    page: 'forge',
    path: '/',
    state: { kind: 'confirm', scene: bakeryScene, interview: 'bakery', entries: bakeryFields },
  },
  // The scene that says less: a want and no gender (ADR-018 decision 6).
  'forge-confirm-market': {
    page: 'forge',
    path: '/',
    state: { kind: 'confirm', scene: marketScene, interview: 'market', entries: marketFields },
  },
  'forge-turned-down': { page: 'forge', path: '/', state: { kind: 'turned-down', scene: bakeryScene } },
  'forge-writing': { page: 'forge', path: '/', state: { kind: 'writing', scene: bakeryScene, interview: 'bakery' } },
  'forge-written': {
    page: 'forge',
    path: '/',
    state: {
      kind: 'written',
      scene: bakeryScene,
      interview: 'bakery',
      character: { kind: 'written', draft: '0', name: bakeryCharacters[0].name, sheet: bakeryCharacters[0].sheet },
    },
  },
  // The dice in the console's own example of this line (docs/architecture.md, "The namer").
  'forge-no-name': {
    page: 'forge',
    path: '/',
    state: { kind: 'no-name', scene: bakeryScene, interview: 'bakery', reason: 'no name followed the dice (K, 2, V)' },
  },
  // The cap's message as SpendingCapReachedException words it, at the console's 200,000 tokens.
  'forge-cap': {
    page: 'forge',
    path: '/',
    state: {
      kind: 'stopped',
      scene: bakeryScene,
      reason: 'Spending cap reached: this session has used 201,342 tokens of 200,000. Nothing more is sent to the model.',
    },
  },
  'forge-saved': { page: 'forge', path: '/', state: { kind: 'saved', scene: bakeryScene, interview: 'bakery', number: 17 } },
  'forge-left': { page: 'forge', path: '/', state: { kind: 'left', scene: bakeryScene } },
};
