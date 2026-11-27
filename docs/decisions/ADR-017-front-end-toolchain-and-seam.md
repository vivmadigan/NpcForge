# ADR-017: The front end is a Vite and React app in `NpcForge.Web`, on made-up data behind one seam

- **Status:** accepted. Amended 2026-11-15: decisions 1, 3 and 6, two defaults and one open item are
  amended for the website by ADR-018. Amended 2026-11-16: decisions 1, 2 and 3, one item left to the
  owner and one consequence are amended by ADR-019. Amended 2026-11-27: decisions 2 and 3 are
  amended by ADR-021 (`npm test` needs Docker, and `rollAndWrite` gains a step callback).
- **Date:** 2026-11-14
- **Supersedes:** no record. It fills in what ADR-014 left open when it named "a React app". It
  amends the INDEX line on tests, which named xUnit alone and now names the front end's own free
  suite beside it.

## Context

Epic 12, "A front end to click through". The owner wants friends to use NpcForge on a website. The
epic builds the website's two pages first, on made-up data with no backend, so that epic 6 can put
logins, a database and `NpcForge.Api` behind them by replacing one seam and not the pages. The
epic's last constraint asks for a design check and an ADR before any page is built. "Simple" is the
owner's word for the epic.

The repo is .NET only: no `package.json`, no CI. No file in `NpcForge.Core`, `NpcForge.Console` or
`NpcForge.Tests` may change, and `dotnet test` must still pass. `NpcForge.slnx` lists three projects,
so a folder it does not list is invisible to `dotnet build` and `dotnet test`. The owner's machine has
Node 24.21.0 and npm 11.3.0.

The design was checked on 2026-11-13. Three runners (Opus, Sonnet and Haiku) each wrote a whole
design from the same grounding, one free to choose, one told to keep the fewest parts, and one told
to build around epic 6's HTTP API. An Opus judge scored them against the epic's "Done when" and
picked the first as the base, 26 of 30 against 24 and 15. The current docs of Vite, Playwright,
react-markdown and axe were read through Context7. Throwaway spikes outside the repo then ran what
the designs rested on. They showed:

- **The Markdown renderer keeps every word.** `react-markdown` 10.1.0 with no plugin rendered all 16
  saves in `NpcForge.Console/characters.json` and the newest sheet from
  `docs/runs/raw/2026-11-08-01-epic4-bakery-roll-again.md`. The words on the page equalled the words
  of the source, in order, for all 17 (word pattern `[\p{L}\p{N}]+`). A gap such as
  `[who keeps stealing the croissants]` stayed plain text, and raw HTML came out escaped. The same
  check then ran in a real browser on the 16 saves at both widths and passed 32 of 32. With only a
  sheet's first paragraph rendered it failed and named the missing words.
- **The contrast check works, and the sketch fails it once.** `@axe-core/playwright` 4.14.0 under
  `@playwright/test` 1.65.0 flagged the sketch's `SAVED: 16` colour, `#8a6a55` on `#120a0a`, at
  3.98:1 against the 4.5:1 that WCAG AA asks of small text. It did so at phone and desktop width,
  passed when that colour was lightened, and reported nothing it could not judge. The sketch's six
  other colour pairs pass.
- **The smallest toolchain runs.** Vite 8.3.8 served a React 19.3.1 and TypeScript 7.0.2 app with no
  `vite.config.ts` and no React plugin. Playwright started that dev server itself, opened a deep path
  (`/saved/3`), and passed at both widths. A font bundled from `@fontsource` loaded, and the page
  made no request to another host. `tsc --noEmit` passed over the app and the tests with the compiler
  options in decision 1, and failed inside Playwright's own type files with `strict` and `jsx` alone.
- **A JSON file needs an import attribute.** Playwright refused `import saves from './saves.json'`
  in a test until it read `with { type: 'json' }`. Vite and `tsc` accept the same line.
- **IM Fell English has one weight.** Its `@fontsource` package ships 400 and 400 italic only. Press
  Start 2P ships 400 only.
- **The start command.** `npm start` runs a `prestart` script first, also through
  `npm --prefix <folder> start`. `npm install` on a folder already installed took 2 to 3 seconds and
  worked offline.
- **Screenshots on request.** A test tagged `@screenshots` ran only under `--grep`, was skipped under
  `--grep-invert`, and wrote its image outside the project folder.
- **What git ignores.** The root `.gitignore` ignores `node_modules/`. It does not ignore Playwright's
  `test-results/`, which appears on every run, or `dist/`. It ignores any folder named `bin`, `obj`,
  `out`, `log`, `logs`, `debug`, `release`, `publish` or `artifacts`, at any depth, among others.

## Options considered

The seam the made-up data sits behind:
- **Real HTTP from day one.** The pages call `fetch` against eight URLs and a streaming endpoint,
  and a dev-server stand-in answers them. Epic 6 would delete the stand-in and change no client code.
  But it fixes the URLs, the request shapes and the stream now, and epic 6's own design check and ADR
  exist to decide "the API the pages from epic 12 need". It is also the most code, and most of it is
  the stand-in that epic 6 deletes.
- **An interface, a made-up implementation and a place that picks one.** Only one implementation
  exists at a time, so the interface is a second copy of the signatures (AGENTS.md, "No abstraction
  until there are two things to abstract").
- **One module of typed functions, `src/api.ts`.** Epic 6 rewrites the bodies. Chosen.

What runs the browser test, and where:
- **Playwright for .NET in a new xUnit project.** `NpcForge.Tests` may not change, so this is a new
  project and an edit to `NpcForge.slnx`. `dotnet test` would then need Node, a dev server and a
  browser, and the 25 second suite would grow.
- **Vitest with jsdom.** It cannot lay out a phone width or measure contrast, so two "Done when"
  lines could not be checked in it, and it would be a second runner beside the browser test.
- **Playwright Test inside the front end's folder.** One runner for the flow, the sheet, the states,
  the contrast and the screenshots. Chosen.

How the sheet is rendered:
- **`marked` or `markdown-it`, a sanitizer, and `dangerouslySetInnerHTML`.** Two packages and a way
  to put raw HTML on the page, for what one package does without it.
- **`react-markdown` with no plugins.** Chosen, and proven on the real saves.

How a state is opened directly:
- **A scenario the made-up API plays.** The pages hold no test code, but most states then need a
  scene typed and a button clicked before they show, so they do not open directly.
- **A third page that lists the states, or Storybook.** The epic allows two pages.
- **A named value of the page's own state, chosen by `?state=<name>`.** Chosen.

Smaller choices, and what lost:
- **`@vitejs/plugin-react` and a `vite.config.ts`.** The judge would keep them, because it is the
  setup every Vite and React example shows and an agent will expect it. The spike showed the app runs
  without them. They are left out as one package and one file that no "Done when" line needs. What is
  lost is Fast Refresh, so an edit reloads the page, and a named state reopens where it was. Add the
  plugin back when that reload gets in the way.
- **A router (React Router, or hash links written by hand).** Two pages and one number in the path.
  `main.tsx` reads `location.pathname`, and links are plain `<a href>`.
- **Committed screenshots compared on every test run.** They could never go stale, but a font or
  rendering difference would fail `npm test` for no change of look. They are for the owner's eyes.
- **A test that fails on a colour outside the tokens file.** The judge suggested it. It is left out
  until a colour slips once; the rule is one sentence and review holds it.
- **Testing a production build instead of the dev server.** Nothing is deployed in epic 12, and the
  dev server is what the one command starts. Epic 6 adds the build when it deploys.
- **The sketch's Google Fonts link.** Every friend's browser would call Google, and the tests would
  need the network. **Font files committed by hand** would work too, as three binaries to fetch and
  keep. The fonts come from npm instead, each under the SIL Open Font License 1.1.
- **Branded id types, Tailwind, CSS modules, a state library, a linter, a mock-server library, a
  root `package.json`, Next.js.** Each adds a concept or a file that no "Done when" line needs.

## Decision

The epic's four defaults hold until the owner decides otherwise. The pages work at phone and
desktop width. Nothing moves or makes a sound, so the wait is a still screen with no spinner. The
name is NpcForge. The rolled brief is shown beside the character.

1. **The toolchain.** A Vite 8, React 19 and TypeScript 7 app in `NpcForge.Web/` at the repo root,
   with npm. The name follows `NpcForge.Console` and epic 6's `NpcForge.Api`. The folder holds no
   `.csproj` and is not in `NpcForge.slnx`. TypeScript is strict and only checks: `tsc --noEmit`.
   Twelve packages, at the versions the spike resolved:

   | Package | Version | What it is for |
   | --- | --- | --- |
   | `react`, `react-dom` | 19.3 | the pages |
   | `react-markdown` | 10.1 | the character sheet |
   | `@fontsource/press-start-2p`, `@fontsource/im-fell-english`, `@fontsource/crimson-text` | 5.3 | the sketch's three typefaces, bundled |
   | `vite` | 8.3 | the dev server |
   | `typescript`, `@types/react`, `@types/react-dom` | 7.0, 19.3 | the type check |
   | `@playwright/test` | 1.65 | the browser tests and the screenshots |
   | `@axe-core/playwright` | 4.14 | the contrast check |

   ```
   NpcForge.Web/
     package.json, package-lock.json
     .gitignore            test-results/, playwright-report/, dist/
     tsconfig.json         the compiler options listed under this map
     playwright.config.ts  starts `npm run dev`; two projects, phone and desktop
     index.html
     src/
       main.tsx            picks the page from the path and a named state from ?state=
       api.ts              the seam (decision 3)
       tokens.css          every colour, typeface and type size (decision 5)
       app.css             every other rule, through var(--...) only
       Frame.tsx           the pixel frame and top bar
       Sheet.tsx           the name, the Markdown sheet, the rolled brief beside it
       ForgePage.tsx       page 1 and its ForgeState
       SavedPage.tsx       page 2 and its SavedState
       madeUp/
         saves.json        the 16 saves: NpcForge.Console/characters.json, copied byte for byte
         bakery.ts         the bakery run: its scene, 19 labelled fields and three characters
         states.ts         every named state of both pages (decision 6)
     tests/
       flow.spec.ts        the whole flow, and a second run that leaves
       sheet.spec.ts       every word of every saved sheet, in order
       states.spec.ts      each named state: the contrast check, and its screenshot
   ```

   `tsconfig.json` sets `strict`, `noEmit`, `"jsx": "react-jsx"` (which Vite reads), `target`
   ES2022, `module` ESNext, `moduleResolution` bundler, `skipLibCheck`, `resolveJsonModule` and
   `"types": ["vite/client"]`, and includes `src`, `tests` and `playwright.config.ts`.

   `saves.json` is the file as it stands at commit `e3ac16e`, which holds 16 saves
   (`git show e3ac16e:NpcForge.Console/characters.json`). It is a file copy, never retyped, because
   eight sheets end lines with two spaces that Markdown reads as a line break, and the word test
   cannot see one dropped. It is imported with `with { type: 'json' }`. It stays at these 16 saves
   when the owner saves more.

   No folder under `NpcForge.Web/` is named `bin`, `obj`, `out`, `log`, `logs`, `debug`, `release`,
   `publish` or `artifacts`, because the root `.gitignore` would hide it. `git check-ignore -v <path>`
   shows whether a new folder is hidden.

2. **The browser test** is Playwright Test, in `NpcForge.Web/tests/`, on Chromium only. Two projects
   set the widths: `phone` at 390 by 844 and `desktop` at 1280 by 800. Its `webServer` runs
   `npm run dev` and reuses a server already on the port. `npm test` runs `tsc --noEmit` and then
   every test but the screenshots. The tests are free: they call no model and no other host. The
   flow test's second run ends with leave and then finds the saved list still at 16. `npm test`
   needs the packages installed, by `npm start` or `npm install`, and its first run downloads the
   browser. `NpcForge.Tests` and `dotnet test` do not change, and `dotnet test` never starts Node.

3. **The seam is `src/api.ts`**, which holds its types and six async functions. It is the only place
   a page gets data. The types are what a page needs, not the JSON the API will send. In epic 12
   the bodies read the made-up data. In epic 6 the same signatures call `NpcForge.Api` and parse its
   JSON into the same types. The sketch below gives the signatures only.

   ```ts
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
     | { kind: 'no-name'; brief: BriefEntry[]; reason: string }   // no name followed the dice
     | Stopped;

   // The name arrives joined, or null for an old save. The rule for joining it stays in the core.
   export type ListedCharacter = { number: number; name: string | null; occupation: string | null; setting: string | null };
   export type SavedCharacter = { number: number; name: string | null; brief: BriefEntry[]; sheet: string };

   // The scene in one go. Returns the next question, the confirmation, or the cap.
   export async function startInterview(scene: string): Promise<InterviewStep>;
   // One answer to the last question (ADR-014: one answer in, the next step out).
   export async function answer(interview: InterviewId, text: string): Promise<InterviewStep>;
   // The yes, and "again": new dice for the same scene and answers, a name, a sheet. Saves nothing.
   export async function rollAndWrite(interview: InterviewId): Promise<RunResult>;
   // Keeps a written character and returns its number.
   export async function save(draft: DraftId): Promise<number>;
   // Every save in number order. Never the text.
   export async function listSaves(): Promise<ListedCharacter[]>;
   // The save under this number, or null.
   export async function loadSave(number: number): Promise<SavedCharacter | null>;
   ```

   - Turning the brief down and leaving are not calls. Nothing is sent, as in the console.
   - Progress while a character is written is not in the seam. No "Done when" line shows progress,
     and whether a run streams or is polled is epic 6's choice.
   - The made-up `startInterview` returns the bakery confirmation for any scene and never reads the
     scene. `rollAndWrite` returns the bakery run's three characters in turn.
   - The made-up saves are the 16 in `madeUp/saves.json`, plus any new save in `sessionStorage`, which
     only `api.ts` writes. So a new save survives the page load of a plain link, as it would on a
     server, and each Playwright test starts with a fresh one.
   - `docs/design/epic-12-api-needs.md`, which the epic asks for, is written from this file: one
     section per function, with the page and states that call it.

4. **The sheet is rendered whole.** `Sheet.tsx` hands the `sheet` string to `react-markdown` with no
   plugins, no `components` and no options. The page never splits, searches or trims it, and its
   look comes from CSS under the sheet's container. The name above the sheet comes from `name`, never
   from the text, and a save without one shows `(no name)` as `--list` does (AGENTS.md, "Never parse
   the model's prose"). `sheet.spec.ts` opens each of the 16 saves and compares two word lists, each
   split by `[\p{L}\p{N}]+`: the text inside the sheet's own container, which holds the rendered
   Markdown and nothing else, and the saved text. Markdown's own marks are not letters, so the lists
   are equal only when every word is on the page, in order, with none added.

5. **Colours and type are set in `src/tokens.css`**, as custom properties on `:root`: every colour
   from the sketch named by its role, the three typefaces and the type sizes. No other file holds a
   colour, a font family or a font size; `app.css` uses `var(--...)`. The three typefaces come from
   the `@fontsource` packages, imported once in `main.tsx`, so Vite serves them from the app's own
   address. The values start as the sketch's, with one change the contrast check forces: the dim
   text colour `#8a6a55` becomes `#a08068`, which passed in the spike.

6. **Each state is a named value, and the app stays at two pages.** Each page holds one value of a
   union type and draws from it: `ForgeState` for the scene page at `/`, and `SavedState` for the
   saved page at `/saved` and, with one character open, `/saved/<number>`. `madeUp/states.ts` is the
   one table of named states, typed against those unions, so a state that no longer fits its page
   fails `tsc`. Each entry holds the path it opens on, such as `/`, `/saved` or `/saved/9`, and the
   state's value, so the table alone gives a state's address. `main.tsx` reads `?state=<name>` and
   gives that value to the page as its starting state. A name the table lacks, or one opened on
   another path than its own, shows "No state named <name>" and not the default page. The table
   holds at least the seven states the epic names: the wait while a character is written, the
   interviewer asking for a missing place or role, the brief turned down, no name found for the
   dice, the spending cap reached, an empty saved list and a long one. `states.spec.ts` takes its list from
   the table's keys, so a new state is checked and photographed with no second list to edit.
   `npm run screenshots` writes `docs/design/epic-12/<state>-<phone|desktop>.png`, full page, after
   the fonts have loaded. `npm test` never rewrites those files.

7. **The contrast check** is axe's `color-contrast` rule through `@axe-core/playwright`. It runs on
   every named state at both widths, and after each step of the flow test, so a screen reached only
   by clicking is checked too. A result axe reports as `incomplete` fails the test as a violation
   does, because "could not tell" is not a pass.

8. **The one command** is `npm start`, run in `NpcForge.Web/`, or `npm --prefix NpcForge.Web start`
   from the repo root. It installs what is missing, starts the dev server on port 5173 and opens the
   browser.

   ```json
   "prestart": "npm install --no-audit --no-fund",
   "start": "vite --port 5173 --strictPort --open",
   "dev": "vite --port 5173 --strictPort",
   "pretest": "playwright install chromium",
   "test": "tsc --noEmit && playwright test --grep-invert @screenshots",
   "screenshots": "playwright test --grep @screenshots"
   ```

   `start` is for a person. `dev` is the same server for Playwright, which must not install or open
   a browser tab on every test run. With `--strictPort`, Vite stops with an error when port 5173 is
   taken. Without it Vite would pick another port, and the tests would look for the app on 5173.

## Why

- **A module of functions for the seam.** It is the smallest thing epic 6 can replace, and it leaves
  the URLs, the stream and the logins to the design check that owns them. A page calls one function
  for each thing the GM does (principle-laziness-protocol, principle-boundary-discipline: the JSON is
  parsed into the page's types in one file).
- **Outcomes as values.** The cap, a missing name and a question are each a `kind` the page must
  draw, so each state the epic lists is a case the compiler knows (principle-model-the-domain).
- **States as values of the page, not of the made-up data.** They are typed against the page and
  open by address. They also still work after epic 6 replaces the seam. On a real API the cap and
  the missing name cannot be reached without a stand-in model.
- **One runner, in the front end's folder.** The test sits beside the code it tests and costs
  `dotnet test` nothing (principle-subtract-before-you-add).
- **The renderer chosen on evidence.** The every-word check ran on the real saves before the choice
  was recorded (principle-prove-it-works).
- **One file for colours and type.** The owner will tune how dark and how red it is, and that should
  be one file to open (principle-minimize-reader-load).
- **Three designs before one.** Two of the three agreed on the folder, the tools, the renderer,
  colours and type as CSS custom properties set once, and a table of named states. That agreement is
  the evidence that the shape is not one model's habit (principle-exhaust-the-design-space).

## Left to the owner

Each has a default, so the build can start. All are taste, so none is decided here.

- **How light the dim text is.** It must pass AA, which starts near `#95725c`. Default: `#a08068`.
- **How a brief field's name reads.** Default: as the console prints it, such as `playersWant`. Plain
  words would be a second list of the fields, or a label the API sends in epic 6.
- **What "a long saved list" is.** Default: the 16 real saves. A longer list would hold saves the
  owner never made.
- **Bold in the sheet.** IM Fell English has no bold weight, so the browser thickens the regular one.
  Default: leave it, and judge it in the first screenshots.
- **A way to start it from Visual Studio or VS Code.** Default: none. The terminal command is the
  one way.
- **Whether the named states and the made-up data outlive epic 6.** Epic 6's design check decides.

## Consequences

- **A second free suite.** `dotnet test` covers the core and the console. `npm test` in
  `NpcForge.Web/` covers the pages. A change to the pages is proved by the second, and neither starts
  the other.
- **Node is now needed to work on the pages**, at a version Vite 8 accepts: 20.19 or later, or 22.12
  or later. The first `npm test` downloads Playwright's Chromium to the user's profile, about 740 MB
  on disk. The design check's spike has already done so on the owner's machine.
- **Text sits on flat colours.** axe cannot judge text over an image or a gradient, and the check
  fails what it cannot judge. A textured parchment behind the sheet would need that rule revisited.
- **One sketch colour changes**, and the contrast check will name any other pair that fails once real
  pages exist. *Needs the owner's eyes*, with how dark, how red and how pixel it is.
- **`NpcForge.Web/.gitignore` is new.** The root `.gitignore` is not edited.
- **AGENTS.md, `docs/architecture.md` and README change in the first build PR**, when the commands
  exist: "Build and run", "Where things are", and the line that names the front ends.
- **Epic 6 inherits** the six signatures as the list of what the pages need, a `rollAndWrite` that
  may have to report progress, a made-up store to delete, and path addresses (`/saved/3`) that its
  server must answer with the app's `index.html`. A page changes only where the real API forces it.
- **First to build:** `NpcForge.Web/` with its `package.json` and `tsconfig.json`, `src/api.ts` with
  `listSaves` and `loadSave` over `madeUp/saves.json`, a bare `SavedPage`, and `sheet.spec.ts`, shown
  failing with only a sheet's first paragraph rendered and then passing. That one slice proves the
  toolchain, the seam and the sheet together.
