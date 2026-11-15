// ADR-017 decision 3: the one place a page gets data. Epic 6 replaces the bodies below with calls
// to NpcForge.Api and keeps these signatures. No page imports from madeUp/.
import { bakeryCharacters, bakeryFields } from './madeUp/bakery';
import { savedCharacters, savedList } from './madeUp/saves';

// A page passes an id back and never reads it.
export type InterviewId = string;
export type DraftId = string;

// The spending cap, at any model call. Nothing was saved.
export type Stopped = { kind: 'stopped'; reason: string };

// One brief field as the confirmation shows it, in the order sent. A field recorded from the
// scene carries the GM's words and the sentence they came from. An answer the GM typed when asked
// has no quote, and the console shows it as "typed when asked" (ADR-010, decision 4). A rolled
// field shows no value before the yes.
export type Quoted = { quote: string; sentence: string };
export type ConfirmField =
  | { field: string; label: 'my words'; value: string; from: Quoted | 'typed when asked' }
  | { field: string; label: 'inferred'; value: string; from: Quoted }
  | { field: string; label: 'not given' | 'rolled'; note: string };

export type InterviewStep =
  | { kind: 'question'; interview: InterviewId; field: string; question: string } // a place or role is missing
  | { kind: 'confirm'; interview: InterviewId; fields: ConfirmField[] }
  | Stopped;

// A rolled or saved brief, in the order sent. An old save has fewer entries (ADR-009).
export type BriefEntry = { field: string; value: string | null };

export type RunResult =
  | { kind: 'written'; draft: DraftId; name: string; brief: BriefEntry[]; sheet: string }
  | { kind: 'no-name'; brief: BriefEntry[]; reason: string } // no name followed the dice
  | Stopped;

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

// The made-up run is the bakery run for any scene, so the confirmation never reads the scene.
const bakeryInterview: InterviewId = 'bakery';

// The scene in one go. Returns the next question, the confirmation, or the cap.
export async function startInterview(_scene: string): Promise<InterviewStep> {
  return { kind: 'confirm', interview: bakeryInterview, fields: bakeryFields };
}

// One answer to the last question (ADR-014: one answer in, the next step out).
export async function answer(_interview: InterviewId, _text: string): Promise<InterviewStep> {
  return { kind: 'confirm', interview: bakeryInterview, fields: bakeryFields };
}

// The bakery run's three characters in turn, then the first again. The draft id is the character's
// place in that list, so `save` keeps the one on the screen.
let written = 0;

// The yes, and "again": new dice for the same scene and answers, a name, a sheet. Saves nothing.
export async function rollAndWrite(_interview: InterviewId): Promise<RunResult> {
  // A real model takes many seconds to roll, name and write. A short wait lets someone clicking
  // through the made-up run see the wait screen, as they will in epic 6.
  await new Promise((resolve) => setTimeout(resolve, 1000));
  const place = written % bakeryCharacters.length;
  written += 1;
  return { kind: 'written', draft: String(place), ...bakeryCharacters[place] };
}

// A new save lives in sessionStorage, read and written only here, so it survives the page load of a
// plain link as it would on a server, and each Playwright test starts with none (ADR-017 decision 3).
const sessionKey = 'npcforge-saves';

function sessionSaves(): SavedCharacter[] {
  return JSON.parse(sessionStorage.getItem(sessionKey) ?? '[]');
}

// Keeps a written character and returns its number.
export async function save(draft: DraftId): Promise<number> {
  const kept = sessionSaves();
  const number = savedList.length + kept.length + 1;
  const { name, brief, sheet } = bakeryCharacters[Number(draft)];
  sessionStorage.setItem(sessionKey, JSON.stringify([...kept, { number, name, brief, sheet }]));
  return number;
}

function briefValue(brief: BriefEntry[], field: string): string | null {
  return brief.find((entry) => entry.field === field)?.value ?? null;
}

// Every save in number order. Never the text. A new array each time, so the 16 made-up saves that
// the saved-long state holds never grow.
export async function listSaves(): Promise<ListedCharacter[]> {
  const kept = sessionSaves().map(({ number, name, brief }) => ({
    number,
    name,
    occupation: briefValue(brief, 'occupation'),
    setting: briefValue(brief, 'setting'),
  }));
  return [...savedList, ...kept];
}

// The save under this number, or null.
export async function loadSave(number: number): Promise<SavedCharacter | null> {
  return [...savedCharacters, ...sessionSaves()].find((character) => character.number === number) ?? null;
}
