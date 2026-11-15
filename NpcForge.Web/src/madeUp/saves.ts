// The made-up saves as typed values, so a named state can hold them without awaiting the seam
// (ADR-017 decision 6). Only api.ts and main.tsx import from madeUp/ (decision 3).
import rawSaves from './saves.json' with { type: 'json' };
import type { ListedCharacter, SavedCharacter } from '../api';

// The save file's own shape stays in this file, so a page sees only the types from api.ts.
type RawSave = {
  brief: Record<string, string | null>;
  text: string;
  name?: { syllables: string[]; family: string };
};
const raw: Record<string, RawSave> = rawSaves;

function joinedName(save: RawSave): string | null {
  return save.name ? `${save.name.syllables.join('')} ${save.name.family}` : null;
}

const numbered = Object.entries(raw)
  .map(([number, save]) => ({ number: Number(number), save }))
  .sort((a, b) => a.number - b.number);

// Every save in number order, as the list shows it. Never the text.
export const savedList: ListedCharacter[] = numbered.map(({ number, save }) => ({
  number,
  name: joinedName(save),
  occupation: save.brief.occupation ?? null,
  setting: save.brief.setting ?? null,
}));

// Every save in number order, whole.
export const savedCharacters: SavedCharacter[] = numbered.map(({ number, save }) => ({
  number,
  name: joinedName(save),
  brief: Object.entries(save.brief).map(([field, value]) => ({ field, value })),
  sheet: save.text,
}));
