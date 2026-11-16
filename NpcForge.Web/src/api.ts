// ADR-017 decision 3: the one place a page gets data. Epic 6 replaces the bodies below with calls
// to NpcForge.Api and keeps these signatures. No page imports from madeUp/.
import { bakeryCharacters, bakeryFields, type BriefEntry } from './madeUp/bakery';
import { savedCharacters, savedList } from './madeUp/saves';

// A page passes an id back and never reads it.
export type InterviewId = string;
export type DraftId = string;

// The spending cap, at any model call. Nothing was saved.
export type Stopped = { kind: 'stopped'; reason: string };

// The brief's field names, in the order the roll lists them (ADR-018 decision 1).
export type BriefField =
  | 'setting' | 'playersWant' | 'occupation' | 'difficulty' | 'characterWants' | 'obstacle'
  | 'attitude' | 'mannerism' | 'weakness' | 'willNotDiscuss' | 'wrongAbout' | 'tone' | 'race'
  | 'nameLetter' | 'nameSyllables' | 'familyLetter' | 'gender' | 'age' | 'look';

// One field the scene settled, in the roll's order, with the value the brief will hold. null is
// "not given" (ADR-010 decision 2). A field the dice will pick has no entry. The page writes the
// sentences from these (src/sentences.ts), so no label, quote or rolled field crosses.
export type ConfirmEntry = { field: BriefField; value: string | null };

export type InterviewStep =
  | { kind: 'question'; interview: InterviewId; field: string; question: string } // a place or role is missing
  | { kind: 'confirm'; interview: InterviewId; entries: ConfirmEntry[] }
  | Stopped;

// No page shows the brief, so it does not cross the seam (ADR-018 decision 9).
export type RunResult =
  | { kind: 'written'; draft: DraftId; name: string; sheet: string }
  | { kind: 'no-name'; reason: string } // no name followed the dice
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
  sheet: string;
};

// The made-up run is the bakery run for any scene, so the confirmation never reads the scene.
const bakeryInterview: InterviewId = 'bakery';

// A real model takes many seconds to roll, name and write, and to read a scene. A short wait lets
// someone clicking through the made-up run see the wait screen, as they will in epic 6.
const aSecond = () => new Promise((resolve) => setTimeout(resolve, 1000));

// The scene in one go. Returns the next question, the confirmation, or the cap.
export async function startInterview(_scene: string): Promise<InterviewStep> {
  // ADR-018 decision 6: the wait, as `rollAndWrite` has, so the flame shows on send.
  await aSecond();
  return { kind: 'confirm', interview: bakeryInterview, entries: bakeryFields };
}

// One answer to the last question (ADR-014: one answer in, the next step out).
export async function answer(_interview: InterviewId, _text: string): Promise<InterviewStep> {
  return { kind: 'confirm', interview: bakeryInterview, entries: bakeryFields };
}

// The bakery run's three characters in turn, then the first again. The draft id is the character's
// place in that list, so `save` keeps the one on the screen.
let written = 0;

// The yes, and "again": new dice for the same scene and answers, a name, a sheet. Saves nothing.
export async function rollAndWrite(_interview: InterviewId): Promise<RunResult> {
  await aSecond();
  const place = written % bakeryCharacters.length;
  written += 1;
  const { name, sheet } = bakeryCharacters[place];
  return { kind: 'written', draft: String(place), name, sheet };
}

// A new save lives in sessionStorage, read and written only here, so it survives the page load of a
// plain link as it would on a server, and each Playwright test starts with none (ADR-017 decision 3).
// A save keeps its brief, because `listSaves` reads the occupation and the setting from it. The brief
// stays inside the made-up code, and no type a page sees holds it (ADR-018 decision 9).
const sessionKey = 'npcforge-saves';

type KeptSave = SavedCharacter & { brief: BriefEntry[] };

function sessionSaves(): KeptSave[] {
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
  const found = [...savedCharacters, ...sessionSaves()].find((character) => character.number === number);
  return found ? { number: found.number, name: found.name, sheet: found.sheet } : null;
}
