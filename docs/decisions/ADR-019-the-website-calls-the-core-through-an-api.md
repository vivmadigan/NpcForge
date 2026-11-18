# ADR-019: The website calls the core through `NpcForge.Api`, with shared adapters and an Aspire AppHost

- **Status:** accepted. Amended 2026-11-18: decision 9's Aspire version is 13.6.5, not 13.4.2
  (ADR-020).
- **Date:** 2026-11-16
- **Supersedes:** no record whole. It amends ADR-014 (the projects, where the adapters live, which
  epic adds the interview's entry and gives the API its DI), ADR-017 (decisions 1, 2 and 3, one
  item left to the owner and one consequence) and ADR-018 (which epic sends the confirmation's
  entries). It changes AGENTS.md's "One core, front ends around it, no further layers".

## Context

Epic 14, "The website makes real characters". The console makes real characters and the website's
two pages run on made-up data. The owner wants them joined on the owner's machine: a scene typed in
the browser returns a real character that can be saved and reopened, and one command starts
everything. No logins, no database and no deploy, which stay in epic 6. The epic asks for a design
check and this record before anything is built. "Simple is good" is the owner's phrase for it.

The design was checked on 2026-11-16. Three runners (Opus, Sonnet and Haiku) each wrote a whole
design from the same grounding: one free to choose, one told to keep the fewest parts, one told to
follow the Aspire and ASP.NET Core docs. An Opus judge scored them 22, 25 and 13 of 30 and picked
the fewest-parts design as the base. The orchestrator leaned to the first. The two differed on
where the adapters live and on what happens to the made-up data, and the owner chose both. The
three designs agreed on the rest, and that agreement is most of this record.

The code showed these facts:

- `SceneInterview.RunAsync` reads a `TextReader` and writes a `TextWriter`
  (`NpcForge.Core/Interview/SceneInterview.cs:24`). A browser sends each answer as its own request.
  `SceneDraft` already holds everything between the steps and is pure.
- `ModelClients`, `FileCharacterStore`, `ReplayChatClient`, `RecordingChatClient` and `Recording`
  are in `NpcForge.Console`. `ModelClients` holds the `NPCFORGE_OFFLINE` stop, and its own comment
  says a web host sets it.
- `FileCharacterStore` reads the whole file, takes one past the highest number and writes the whole
  file with `File.WriteAllText`. It takes no lock, and `SaveAsync` runs synchronously. Its
  `DefaultPath` is three folders up from the running program, which is the owner's file only for
  the console.
- `RunOptions.cs:175-180` refuses `--record` for a run with no answer flags, and both recorded
  fixtures are flag runs. So no recording of a scene interview exists.
- Only the top bar's count calls the seam from a named state (`Frame.tsx:52`). Every other named
  state holds its own data in `src/madeUp/states.ts`.
- `src/madeUp/saves.json` is `NpcForge.Console/characters.json` copied byte for byte (ADR-017,
  decision 1), so it is already in the file store's format.
- The scene box is a `textarea`, so a scene from the browser can hold line breaks. A console scene
  is one line.

A throwaway spike outside the repo ran Aspire 13.4.2 against a copy of `NpcForge.Web` with its
scripts unchanged, a four-line `vite.config.ts` and a minimal API. It showed:

- **Aspire starts the Vite app as it is.** `AddViteApp("web", "../web").WithReference(api)` ran
  `npm install`, then the `dev` script with its own port added at the end. Vite's arguments were
  `--port 5173 --strictPort --port 49201`, and Vite listened on 49201 while another process held
  5173. So the scripts need no change. All three designs assumed the opposite.
- **The proxy works.** Vite's environment held `API_HTTP`, `GET /api/saves` through Vite returned
  the API's JSON, and `GET /saved/3` returned the app's page.
- **Stopping the AppHost stopped Vite and the API.** No Docker was needed.
- **Not checked:** the same behaviour on Aspire 13.6.5, the current version, whose restore failed
  twice on the network and then passed. Nor that the dashboard shows the API's stderr as its log,
  which the Aspire docs say it does.

## Options considered

Where the model clients and the file store live:
- **The API references the console project.** Nothing moves, and it is one PR fewer. The judge chose
  it. But the website would depend on the console, and `Lab` and `RunOptions` would be reachable
  from the API. Its case rests on epic 6 replacing both adapters. A database replaces the file
  store. The API still needs the vendor clients.
- **A copy in each front end, or linked source files.** The file's byte-exact rules and the offline
  stop would live twice, and `NpcForge.Tests` would see `ModelClients` defined twice.
- **A new project, `NpcForge.Adapters`.** ADR-014 set its trigger: "The project is added when a second
  front end needs the same adapter." Chosen by the owner.

The core's entry for one answer at a time:
- **A new `SceneSession` class over the draft, with step records.** It forwards `Lines()` and
  `RollArguments()` to `SceneDraft` and hides nothing.
- **Step records on the draft (`Question`, `Confirm`).** Two types for what a null already says.
- **One method pulled out of `RunAsync`, and three members on `SceneDraft`.** Chosen.

The made-up data:
- **Two paths: the made-up bodies kept behind `npm run dev:made-up` on a second dev server.** No
  browser test changes. But the seam keeps two implementations in step, which ADR-017 rejected,
  and the flow tests go on testing made-up data.
- **A Playwright fixture that answers `/api` with made-up JSON.** A third copy of the wire format.
- **One path.** The made-up bodies leave `api.ts`, and each named state keeps its own data. Chosen
  by the owner.

Which saves the site shows:
- **A file of the site's own.** No lock between processes. The owner's 16 characters would not be
  on the saved page.
- **The owner's `NpcForge.Console/characters.json`, with a lock.** Chosen by the owner.

The stand-in model for the tests:
- **One paid recording of a scene run.** The console refuses to record one today, so it needs a
  console change first, and a re-record after any change to a skill.
- **A scripted fake.** Free, and the tests already script the interviewer and the namer. Chosen.

How the browser test reaches the API:
- **A second command for the live flow**, so `npm test` stays Node only. Two commands to remember.
- **Aspire's test host.** It picks its own ports and turns the dashboard off, so it tests a
  different arrangement from the one the owner runs.
- **Playwright starts its own API and its own dev server.** Chosen.

What Aspire adds:
- **An AppHost and `NpcForge.ServiceDefaults`.** Structured traces and health checks, which serve a
  deploy. The dashboard shows each program's log without it.
- **Keys as Aspire parameters.** A prompt on first run, for keys the owner has already set.
- **An AppHost and no more.** Chosen.

## Decision

1. **Six projects in the solution, three of them new, and the website beside them.**

   ```
   NpcForge.slnx
   ├── NpcForge.Core       every rule the front ends share. Its references do not change
   ├── NpcForge.Adapters   new. ModelClients, FileCharacterStore, ReplayChatClient,
   │                       RecordingChatClient, Recording, and the vendor packages
   ├── NpcForge.Console    Program.cs, RunOptions, KeepQuestion, SaveList, Lab, LabSheet
   ├── NpcForge.Api        new. Program.cs, Forge, the wire types, StandInChatClient
   ├── NpcForge.AppHost    new. AppHost.cs
   └── NpcForge.Tests      gains a reference to NpcForge.Api and Microsoft.AspNetCore.Mvc.Testing
   NpcForge.Web/           outside the solution, as now
   ```

   `NpcForge.Adapters` references the core and the vendor packages, and never a front end. A test
   beside `CoreReferenceTests` holds that. The five files move whole and keep the namespace
   `NpcForge`, so no test's assertion changes. One line joins the stale-build check in `ConsoleProcess.cs`,
   which names the projects it watches (lessons 013 and 016). The default provider and model move
   from `RunOptions`' flag table to `ModelClients.DefaultProvider` and `DefaultModel`, which the
   table reads. `ModelClients` moves with its replay branch and its offline
   stop, so the API has both. `FileCharacterStore.DefaultPath` moves with its class and stays the
   console's rule. The API never reads it.

2. **The core gains one entry and four small members.** No new type.

   ```csharp
   // SceneInterview.cs. Lines 37 to 63 of today's RunAsync, moved: the model's turns over the
   // scene, the turn-cap catch and the [skill] and [interview] traces. Returns the draft.
   public static Task<SceneDraft> ReadAsync(
       IChatClient client, ChatOptions options, IReadOnlyList<RollField> fields, string scene, CancellationToken ct);

   // SceneDraft.cs. Still pure.
   public string Scene { get; }                    // as the GM typed it, for the writer
   public RollField? NextQuestion { get; }         // the first of StillToAsk; null means ready to confirm
   public RecordResult Answer(string typed);       // RecordAsked for NextQuestion; refused when nothing is asked
   public IReadOnlyList<ConfirmationLine> Settled();   // Lines() without the rolled fields

   // ModelOptions.cs. Program.cs:146-151, moved, so the API cannot drift from the console.
   public static class ModelOptions { public static ChatOptions New(string modelId); }
   ```

   `RunAsync` keeps its signature, its screen and its traces, and drives these same members, so
   `SceneInterviewTests` passes unchanged. `RollArguments()` is built from `Settled()`, so what the
   page confirms is what is rolled. Answering a question calls no model. The website's confirmation
   (ADR-018) is `Settled()` as a field name and a value, with null for a want not given.

3. **`NpcForge.Api` is a minimal API with six routes**, one for each function in `src/api.ts`.

   | Route | Returns |
   | --- | --- |
   | `POST /api/interviews` with the scene | a question, the confirmation, or the cap |
   | `POST /api/interviews/{id}/answers` with the text | the same three |
   | `POST /api/interviews/{id}/characters` | a written character, no name, or the cap |
   | `PUT /api/drafts/{id}/save` | the save's number |
   | `GET /api/saves` | every save's number, joined name, occupation and setting |
   | `GET /api/saves/{number}` | the save's joined name and sheet, or 404 |

   One singleton, `Forge`, owns every interview and every draft in memory until the process stops.
   An interview holds its draft, one `Dice` and one `SpendingCap`, so a roll again takes the next
   dice, as in the console. A gate lets one request at a time work on an interview, because `Dice`
   and the cap are not safe across threads. A draft holds the written character and the task of its
   first save, so a second save of one draft waits for the first and returns its number. Nothing
   expires. Options are built for each request by `ModelOptions.New`.

   The JSON is the API's own types, private to it. No core type is serialised, so a rename in the
   core cannot rename a field the page reads. The key is read at the first model call, so the
   saved page works with no key, as `--list` does. `ToneFiles.Require` and `NotGivenFiles.Require`
   run when the API starts. The core's traces stay on stderr.

4. **Outcomes are bodies and faults are statuses.**

   | What happened | The API answers | The page shows |
   | --- | --- | --- |
   | The spending cap | 200, `kind: 'stopped'` and the cap's message | the cap state, as now |
   | No name followed the dice | 200, `kind: 'no-name'` and the console's words | the no-name state, as now |
   | An interview or draft the API does not hold | 404 | the failure state, with the scene kept |
   | A roll the dice refuse | 422 with the reason | the failure state |
   | A missing key, or the offline stop | 503 with `ModelClients`' message | the failure state. The saved page still works |
   | A save file that will not parse | 500 with the store's message | the failure state |
   | No answer from the API | none | the failure state |

   Faults use ASP.NET Core's problem details. `api.ts` throws one error type for every fault, and
   each page gains one `failed` state that shows the message. The 503 is mapped only around the
   call to `ModelClients.Create`, because its error type is one a bug elsewhere also throws. Two named states, `forge-failed` and
   `saved-failed`, put it under the contrast check.

5. **The site shows the owner's saves, behind a lock.** The AppHost passes the API the path of
   `NpcForge.Console/characters.json`, unless `NPCFORGE_CHARACTERS` is already set, which is how an
   agent points a run at a scratch file. The API refuses to start with no path. `FileCharacterStore`
   opens the file for itself alone across the read, the number and the write, and a reader that
   finds it held waits and tries again. The lock is the operating system's, so it holds between
   the console and the API. A save from the site changes the tracked file as a console save does,
   and AGENTS.md's "My saves" covers it.

6. **The site calls the console's model, with the cap counted for each interview.** The API's
   provider and model default to `ModelClients.DefaultProvider` and `DefaultModel`, which
   `RunOptions` also reads.
   The API's project carries the console's `UserSecretsId`, so the owner sets no new key. Each
   interview gets `SpendingCap.TokensPerSession`, 200,000 tokens. Any number of interviews can be
   started, so there is no ceiling across them until epic 6's caps.

7. **The stand-in model is a scripted `IChatClient` in `NpcForge.Api`**, picked by the provider
   setting `stand-in`. It records what its script finds in the scene, names inside the dice it is
   sent and writes a fixed sheet, and it reports tokens so a test can reach the cap. The namer's
   rule has one home: the tests' `NamerStandIn` calls it. The API picks it before it calls `ModelClients.Create`, which knows no such provider, so the
   console's providers do not change.

8. **The website keeps one path.** The six signatures in `src/api.ts` stay and their bodies call
   `/api`. The made-up bodies, the `sessionStorage` saves and the one-second waits are deleted.
   `src/madeUp/` stays for the named states, which hold their own data. A new `vite.config.ts`
   proxies `/api` to `API_HTTP`, or to the API's own port when Aspire did not start it. The npm
   scripts keep `--port 5173 --strictPort`. `@types/node` joins the dev packages, because the two
   config files read the environment. `api.ts` holds the brief's field names as one list, and its
   type comes from that list.

   Between the thin slice and the last PR the forge page still runs on made-up data. Its made-up
   saves stay in `sessionStorage`, and `listSaves` and `loadSave` read them after the real ones, so
   the flow test keeps passing. The last
   PR deletes them.

9. **Aspire adds `NpcForge.AppHost` and no more.** It is in the solution, so Visual Studio can
   start it. It holds the API as a project and the pages through `AddViteApp`, with
   `WithReference(api)` and `WaitFor(api)`. The one command is
   `dotnet run --project NpcForge.AppHost`. `npm --prefix NpcForge.Web start` still serves the
   pages on port 5173, and with no API running each call shows the failure state. The thin-slice
   PR pins the Aspire version it was checked on.

10. **The tests.**
    - The API's tests live in `NpcForge.Tests` and run the API in-process, with the stand-in and a
      temp saves file. They start no process, so they sit in the fast part. The test host is keyed on a type
      inside the API, such as `Forge`, because the console has a `Program` too.
    - The equality check runs one seed and one scene twice: through the core called as `Program.cs`
      calls it (`RunAsync` on a reader, then two rolls on one `Dice`), and through the API. A
      recorder around the stand-in keeps every request, and the test compares
      `Recording.Describe` of each, as `ReplaySensitivityTests` does.
    - The double-save test sends two saves of one draft at once. The two-stores test puts two
      `FileCharacterStore`s on one file and saves from real threads, because `SaveAsync` is
      synchronous and `Task.WhenAll` alone would run them in turn and pass with the lock removed
      (lesson 022).
    - The field-name test reads `NpcForge.Web/src/api.ts` as text, found from the test's own source
      path (lesson 015), and holds its list equal to `Dice.Fields` in order.
    - Playwright starts its own API on the stand-in, with `NPCFORGE_OFFLINE=1`, a fixed seed and a
      temp saves file copied from `src/madeUp/saves.json`. It starts its own dev server. Both take ports of their own, never 5173 and never the
      API's own port, and neither reuses a server already running (lesson 018). The named states
      run on that dev server, where the top bar's count starts at the 16 seeded saves.
    - The failure test makes the API not answer by aborting the page's `/api` requests.

11. **Seven PRs, in this order.** Each is a restructure or a behavior change, and ends in a check.

    | # | PR | Kind | Its check |
    | --- | --- | --- | --- |
    | 1 | This record | Docs | The owner's review |
    | 2 | `NpcForge.Adapters`: the five files, their packages and the default provider and model move | Restructure | Replay and roll-again on the unchanged fixtures, and no changed assertion |
    | 3 | The thin slice: the API's two save routes, the AppHost, `vite.config.ts`, the saved page on `/api`, Playwright's two servers | Behavior | The API's list and load tests, the saved page's browser test, and one command (*needs the owner's eyes*) |
    | 4 | The file store's lock | Behavior | The two-stores test, shown failing with the lock removed |
    | 5 | The interview in steps, and `ModelOptions.New` | Restructure | `SceneInterviewTests` unchanged, replay and roll-again unchanged |
    | 6 | `Forge`, the four other routes, the stand-in, and `api.ts`'s field names as one list | Behavior | The walk, equality, double-save, cap and field-name tests, each guard shown failing |
    | 7 | The forge page calls the API, the failure state, the browser tests that count saves (`flow`, `header` and `sheet`) rewritten to check the save each made, the close-out | Behavior | `npm test`, `dotnet test`, and the owner's paid run (*needs the owner's eyes*) |

    The epic listed six tasks. The lock is the seventh PR, split from the slice because it changes
    how the console's store behaves.

## Why

- **A shared project for the adapters.** ADR-014 set the trigger and it has fired. The API needs
  the vendor clients after epic 6 too, so the project is not there for one epic. The owner wants
  the back end's parts in projects of their own.
- **The entry is one method and four members.** `SceneDraft` already was the object that lives
  between steps. A session class or step records would be a second home for its rules
  (principle-laziness-protocol).
- **One owner for an interview.** The page keeps asking one interview for new dice, so its draft,
  dice and cap live and die together in one place.
- **A draft is saved once, by construction.** The second request finds the first save's task and
  waits for it, so a retry or a double click converges on one save
  (principle-make-operations-idempotent).
- **One path through the seam.** A second implementation of `api.ts` is a second thing to keep
  right, and the flow test should prove the path the owner uses.
- **Playwright's own servers, never reused.** A server left running is the one a reusing test
  runs against (lesson 018), and after this epic that server could be calling a paid model.
- **The thin slice first.** Listing saves needs no model and no interview. It proves the API, the
  proxy, the AppHost and the browser test's servers before anything is built on them
  (principle-sequence-verifiable-units).
- **The scripts are left alone.** The spike measured that Aspire's port wins. Moving the port into
  the config would fix a problem that is not there.

## Left to the owner

Each has a default, so the build can start.

- **A scene with line breaks.** Default: sent as typed, with only the ends trimmed (ADR-008, word
  for word). The other choice joins the lines with spaces, so the model sees what a console GM
  could have typed.
- **How the failure state looks and what it says.** Default: the frame, the message as sent, and a
  button that keeps the scene. *Needs the owner's eyes.*
- **Whether the AppHost stays in the solution.** Default: yes. The thin-slice PR measures what it
  adds to `dotnet test`. If that is more than a few seconds, it moves outside, like `NpcForge.Web`.

## Consequences

- **AGENTS.md changes in the PR that makes each line true.** In PR 2, "One core, front ends around
  it, no further layers" becomes "One core, the adapters two front ends share, and front ends
  around them. An adapter one front end uses stays in that front end." In PR 3, the line on DI
  names epic 14, and "Build and run" and "Where things are" gain the new commands and projects.
- **ADR-014, amended.** The solution has six projects, and the shared one is `NpcForge.Adapters`,
  not the "Infrastructure" it weighed. `NpcForge.Api` arrives in epic 14, not epic 6, and gets its
  DI then. The interview's entry arrives in epic 14, and progress reporting stays in epic 6.
- **ADR-017, amended.** Decision 1: a `vite.config.ts` exists and `@types/node` is a thirteenth
  package. Decision 2: `npm test` builds and starts `NpcForge.Api`, so it needs the .NET SDK, and
  its first run waits for a cold build. `dotnet test` still never starts Node. Decision 3: the
  bodies call the API from epic 14, and the made-up store is deleted. "Left to the owner": the
  named states stay on their own data.
- **ADR-018, amended.** The API sends the confirmation's entries from epic 14, and the page writes
  its sentences from them then (decisions 2 and 3). The field-name test settles what holds
  `BriefField` to the core, which ADR-018's Consequences left to epic 6's design check.
- **Lesson 018 stays true for `npm start`.** The browser tests no longer reuse a server.
- **A restart of the API forgets every interview.** The page then gets a 404 and its failure state.
  A reload already loses the run.
- **The API's log mixes two interviews at once.** One person makes that rare. Epic 6 moves the
  traces.
- **The test store is shared by one run's tests.** A browser test asserts the save it made, not a
  total.
- **Running the AppHost calls a paid model.** An agent runs it only on the stand-in and with
  `NPCFORGE_CHARACTERS` set to a scratch file.
- **A fake model ships in the API.** It is free, like `replay`, and only a setting picks it.
- **Epic 6 inherits** an API to put logins on, a `Forge` whose interviews need an owner, a store
  port the database implements, and the per-interview cap to replace.
