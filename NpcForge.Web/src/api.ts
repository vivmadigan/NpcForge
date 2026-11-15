// ADR-017 decision 3: the one place a page gets data. Epic 6 replaces the bodies below with calls
// to NpcForge.Api and keeps these signatures. No page imports from madeUp/.
import rawSaves from './madeUp/saves.json' with { type: 'json' };

// A rolled or saved brief, in the order sent. An old save has fewer entries (ADR-009).
export type BriefEntry = { field: string; value: string | null };

// The name arrives joined, or null for an old save. The rule for joining it stays in the core.
export type ListedCharacter = {
  number: number;
  name: string | null;
  occupation: string | null;
  setting: string | null;
};
export type SavedCharacter = {
  number: number;
  name: string | null;
  brief: BriefEntry[];
  sheet: string;
};

// The save file's own shape stays in this file, so a page sees only the types above.
type RawSave = {
  brief: Record<string, string | null>;
  text: string;
  name?: { syllables: string[]; family: string };
};
const saves: Record<string, RawSave> = rawSaves;

function joinedName(raw: RawSave): string | null {
  return raw.name ? `${raw.name.syllables.join('')} ${raw.name.family}` : null;
}

// Every save in number order. Never the text.
export async function listSaves(): Promise<ListedCharacter[]> {
  return Object.entries(saves)
    .map(([number, raw]) => ({
      number: Number(number),
      name: joinedName(raw),
      occupation: raw.brief.occupation ?? null,
      setting: raw.brief.setting ?? null,
    }))
    .sort((a, b) => a.number - b.number);
}

// The save under this number, or null.
export async function loadSave(number: number): Promise<SavedCharacter | null> {
  const raw = saves[String(number)];
  if (!raw) return null;
  return {
    number,
    name: joinedName(raw),
    brief: Object.entries(raw.brief).map(([field, value]) => ({ field, value })),
    sheet: raw.text,
  };
}
