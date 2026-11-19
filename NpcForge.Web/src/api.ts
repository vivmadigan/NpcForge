// ADR-017 decision 3: the one place a page gets data. Every function here calls one route of
// NpcForge.Api through the dev server's /api proxy (vite.config.ts), and returns the API's answer
// alone (ADR-019, decisions 3 and 8). Outcomes are bodies; every fault, or no answer at all, throws
// the one ApiError, which each page shows in its failure state (decision 4). No page imports the
// made-up data, which only the named states hold.

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

// A request that sends a body sends it as JSON, which is what the API's minimal routes read.
const sending = (method: 'POST' | 'PUT', body?: object): RequestInit => ({
  method,
  headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
  body: body === undefined ? undefined : JSON.stringify(body),
});

// An id goes into a path, so it is escaped there, though the API makes them of letters and digits.
const at = (id: string) => encodeURIComponent(id);

// The scene in one go. Returns the next question, the confirmation, or the cap.
export async function startInterview(scene: string): Promise<InterviewStep> {
  return call('/api/interviews', sending('POST', { scene }));
}

// One answer to the last question (ADR-014: one answer in, the next step out).
export async function answer(interview: InterviewId, text: string): Promise<InterviewStep> {
  return call(`/api/interviews/${at(interview)}/answers`, sending('POST', { text }));
}

// The yes, and "again": new dice for the same scene and answers, a name, a sheet. Saves nothing.
export async function rollAndWrite(interview: InterviewId): Promise<RunResult> {
  return call(`/api/interviews/${at(interview)}/characters`, sending('POST'));
}

// Keeps a written character and returns its number. A second save of one draft returns the first
// save's number and saves nothing more (ADR-019 decision 3).
export async function save(draft: DraftId): Promise<number> {
  const { number } = await call<{ number: number }>(`/api/drafts/${at(draft)}/save`, sending('PUT'));
  return number;
}

// Every save in number order. Never the text.
export async function listSaves(): Promise<ListedCharacter[]> {
  return call('/api/saves');
}

// The save under this number, or null when the API holds none.
export async function loadSave(number: number): Promise<SavedCharacter | null> {
  try {
    return await call<SavedCharacter>(`/api/saves/${number}`);
  } catch (fault) {
    if (fault instanceof ApiError && fault.status === 404) return null;
    throw fault;
  }
}
