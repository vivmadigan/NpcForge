// ADR-017 decision 3: the one place a page gets data. Epic 6 replaces the bodies below with calls
// to NpcForge.Api and keeps these signatures. No page imports from madeUp/.
import { savedCharacters, savedList } from './madeUp/saves';

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

// Every save in number order. Never the text.
export async function listSaves(): Promise<ListedCharacter[]> {
  return savedList;
}

// The save under this number, or null.
export async function loadSave(number: number): Promise<SavedCharacter | null> {
  return savedCharacters.find((character) => character.number === number) ?? null;
}
