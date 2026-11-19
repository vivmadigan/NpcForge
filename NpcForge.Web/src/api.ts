// ADR-017 decision 3: the one place a page gets data. Epic 14 replaces the bodies below with calls
// to NpcForge.Api and keeps these signatures, one PR at a time (ADR-019, decision 11). The saves are
// real now: `listSaves` and `loadSave` read the API through the dev server's /api proxy
// (vite.config.ts). The forge page's four calls still answer from madeUp/, and its made-up saves
// live in sessionStorage after the real ones, until the forge page calls the API. No page imports
// from madeUp/.
import { bakeryCharacters, bakeryFields, type BriefEntry } from './madeUp/bakery';

// A page passes an id back and never reads it.
export type InterviewId = string;
export type DraftId = string;

// The spending cap, at any model call. Nothing was saved.
export type Stopped = { kind: 'stopped'; reason: string };

// The brief's field names, in the order the roll lists them (ADR-018 decision 1). One list, and the
// type comes from it, so a .NET test can read the names as text and hold them equal to the roll's own
// (ADR-019 decision 8). Keep the list a plain array of quoted names.
export const briefFields = [
  'setting', 'playersWant', 'occupation', 'difficulty', 'characterWants', 'obstacle',
  'attitude', 'mannerism', 'weakness', 'willNotDiscuss', 'wrongAbout', 'tone', 'race',
  'nameLetter', 'nameSyllables', 'familyLetter', 'gender', 'age', 'look',
] as const;
export type BriefField = (typeof briefFields)[number];

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

// Every fault from the API, as one type that carries the problem's detail (ADR-019 decision 4). Each
// page shows it in its failure state. A 404 for a save is not a fault the saved page shows, so
// `loadSave` reads it as "none". `status` is 0 when no answer came at all.
export class ApiError extends Error {
  constructor(
    readonly status: number,
    detail: string,
  ) {
    super(detail);
  }
}

// The API's faults are problem details, and the answer of anything in front of it, such as the dev
// server's proxy when nothing listens behind it, may have no JSON body at all.
async function faultOf(response: Response): Promise<ApiError> {
  const problem: { detail?: unknown } | null = await response.json().catch(() => null);
  return new ApiError(
    response.status,
    typeof problem?.detail === 'string' ? problem.detail : `${response.status} ${response.statusText}`,
  );
}

// fetch rejects only when no answer came, such as a dropped connection, and its error names no
// status. It becomes the same ApiError, so a page catches one type (ADR-019 decision 4, its last row).
async function call<Body>(path: string, init?: RequestInit): Promise<Body> {
  let response: Response;
  try {
    response = await fetch(path, init);
  } catch {
    throw new ApiError(0, 'The API did not answer.');
  }
  if (!response.ok) throw await faultOf(response);
  return response.json();
}

const getJson = <Body>(path: string) => call<Body>(path);

const realSaves = () => getJson<ListedCharacter[]>('/api/saves');

// A new save lives in sessionStorage, read and written only here, so it survives the page load of a
// plain link as it would on a server, and each Playwright test starts with none (ADR-017 decision 3).
// A save keeps its brief, because `listSaves` reads the occupation and the setting from it. The brief
// stays inside the made-up code, and no type a page sees holds it (ADR-018 decision 9). The forge page
// calls the API instead in epic 14's last PR, and this store goes then.
const sessionKey = 'npcforge-saves';

type KeptSave = SavedCharacter & { brief: BriefEntry[] };

function sessionSaves(): KeptSave[] {
  return JSON.parse(sessionStorage.getItem(sessionKey) ?? '[]');
}

// Keeps a written character and returns its number: after the highest real save and every kept one,
// so a made-up save never takes the number of a save the API holds (ADR-019 decision 8).
export async function save(draft: DraftId): Promise<number> {
  const kept = sessionSaves();
  const highest = Math.max(0, ...(await realSaves()).map(({ number }) => number));
  const number = highest + kept.length + 1;
  const { name, brief, sheet } = bakeryCharacters[Number(draft)];
  sessionStorage.setItem(sessionKey, JSON.stringify([...kept, { number, name, brief, sheet }]));
  return number;
}

function briefValue(brief: BriefEntry[], field: string): string | null {
  return brief.find((entry) => entry.field === field)?.value ?? null;
}

// Every save in number order: the API's, then the kept ones. Never the text.
export async function listSaves(): Promise<ListedCharacter[]> {
  const kept = sessionSaves().map(({ number, name, brief }) => ({
    number,
    name,
    occupation: briefValue(brief, 'occupation'),
    setting: briefValue(brief, 'setting'),
  }));
  return [...(await realSaves()), ...kept];
}

// The save under this number, or null. A kept save is looked for only after the API says it holds none.
export async function loadSave(number: number): Promise<SavedCharacter | null> {
  try {
    return await getJson<SavedCharacter>(`/api/saves/${number}`);
  } catch (fault) {
    if (!(fault instanceof ApiError && fault.status === 404)) throw fault;
  }
  const found = sessionSaves().find((character) => character.number === number);
  return found ? { number: found.number, name: found.name, sheet: found.sheet } : null;
}
