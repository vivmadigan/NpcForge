# ADR-014: One core, called in-process, with saves behind a port; the MCP server retires

- **Status:** accepted; decision 2's numbering promise, decision 7's checks and decision 9.4's plan for `Storage` amended by ADR-015
- **Date:** 2026-11-08
- **Supersedes:** ADR-012 (the console starts the server's launcher). It replaces three of AGENTS.md's
  rules: "Two processes, no shared contracts project", "No Clean Architecture layering" and "No
  abstraction until there are two things to abstract", where the core needs a port. The rest of
  ADR-003 and ADR-013 stands: a save is the brief, the name and the text, loaded by number, and only
  `save` saves.

## Context

Epic 5, "Ready to grow". Today the dice, the brief, the tables and the save file live in
`NpcForge.Server`, a second process the console starts and talks to over MCP. The namer, the writer,
the interview and `CharacterRun` live in `NpcForge.Console`. A website in epic 6 would have to start
and supervise that second process, and could not reuse the writer. The owner plans the website as a
React app in the browser that talks over HTTP to an ASP.NET Core API, so the API is the second front
end that calls the core.

The design was checked on 2026-11-08 against `Program.cs`, `CharacterRun.cs`, `ChatAgent.cs`,
`McpToolSource.cs`, `IToolSource.cs`, `CharacterTools.cs`, `Storage.cs`, `CharacterBrief.cs`, the
three project files and the tests that use them. Three runners (Opus, Sonnet and Haiku) each sketched
a whole design, and a Sonnet judge scored them blind and picked the one this record is built on. The
code showed these facts:

- The server's dice are one `static readonly Random`, seeded from `NPCFORGE_SEED`. The console starts
  one server per run, so one `Random` per run is the real behaviour, and a roll again relies on it.
  In-process, a static `Random` would be shared by every run and every web request.
- The writer's first message ends with the brief as the MCP server serialised it. The interview's
  fields come from `roll_character`'s JSON schema (`RollField.ListFrom`). The MCP SDK builds that
  schema with `AIFunctionFactory` from Microsoft.Extensions.AI, which the core may use. The server
  resolves Microsoft.Extensions.AI.Abstractions 10.8.3 and the console 10.10.0 (each project's
  `obj/project.assets.json`).
- The model sees one server tool, `lookup_archetype`. Roll, save, load and list are hidden from it by
  a hand-kept list, `McpToolSource.AppOnly`.
- 12 test files use `McpToolSource`, 9 reference `NpcForge.Server`, and tests pass
  `WriterInput.DefaultSkillDir`, a folder path, to file reads. No test calls `CharacterRun`.
- `GetReferencedAssemblies()` lists only the assemblies the compiled code uses. A package added and
  never used does not appear in it.

## Options considered

How many projects:
- **Domain, Application, Infrastructure and a front end.** The textbook Clean Architecture split.
  Here the domain and the application would hold about 2,500 lines with no reader that needs one
  without the other, and even the roll uses `AIFunctionFactory`, so the split enforces nothing the
  core boundary does not already enforce.
- **Core, Infrastructure and Console.** Infrastructure would hold the file store, the vendor clients
  and the record and replay clients. Each has one caller, the console. The website brings its own
  database store and registers its own model client through DI, so the project would still have one
  caller after epic 6.
- **Core and Console.** Every shared rule in the core; every adapter in the front end that uses it.
  Two runners of three chose this.

How the core reaches the skill files:
- **Embedded resources in the core.** No file read at all. But the lab reads skill folders from disk,
  `WriterInput.DefaultSkillDir` disappears, and about ten tests change beyond their `using` lines,
  which the restructure rule forbids. The `[skill]` trace line changes.
- **Skill text as records, loaded by the front end.** The core reads nothing. The same ten tests
  change, and `WriterInput` changes shape.
- **One door in the core, `SkillFiles`, that reads the bundled skills and the lab's folders.** The
  only file read in the core, in one file, by name in the banned-symbols list. No signature changes.

The MCP server:
- **Kept as a thin adapter over the core.** No caller remains after the switch. It would keep a second
  copy of `roll_character`'s seventeen described parameters, an MCP package pinned at 2.2.0, process
  tests, and a door to every save that epic 6's logins would also have to guard.
- **Retired.** All three runners chose this.

A port for the GM's dialogue (`IGmChannel`: trace, say, read):
- The interview already takes a `TextReader` and a `TextWriter`, which are .NET's own abstractions.
  The keep question is the console's. A web request cannot wait on a reader, but what replaces it is
  epic 6's design, and a port shaped now would be a guess.

## Decision

1. **Three projects.** The MCP server is deleted in the behavior PR.

   ```
   NpcForge.slnx
   ├── NpcForge.Core      class library: every rule the front ends share
   │   ├── NpcForge.Core.csproj    Microsoft.Extensions.AI.Abstractions, BannedApiAnalyzers (build only)
   │   ├── BannedSymbols.txt       what the core may not call (decision 7)
   │   ├── Brief/      CharacterBrief.cs (+ enums), Dice.cs, Tables.cs, Looks.cs, RollField.cs, BriefJson.cs
   │   ├── Interview/  SceneInterview.cs, SceneDraft.cs
   │   ├── Writing/    CharacterRun.cs, ChatAgent.cs, Namer.cs, NameCheck.cs, WriterInput.cs, Archetypes.cs,
   │   │               ToneFiles.cs, NotGivenFiles.cs, SkillFiles.cs, Fingerprint.cs
   │   ├── Loop/       AgentLoop.cs, IToolSource.cs, ConsoleTool.cs, SpendingCap.cs
   │   ├── Saves/      ICharacterStore.cs, SavedCharacter.cs (+ CharacterName, ListedCharacter), SavedView.cs
   │   └── skills/     npc-writer/, namer/, scene-interviewer/   (content, copied to each front end's bin\skills)
   ├── NpcForge.Console   exe: the GM's terminal, and the adapters only it uses
   │   ├── Program.cs, RunOptions.cs, KeepQuestion.cs, SaveList.cs, Lab.cs, LabSheet.cs
   │   ├── ModelClients.cs, RecordingChatClient.cs, ReplayChatClient.cs, Recording.cs
   │   ├── FileCharacterStore.cs
   │   └── characters.json         the owner's saves, moved from NpcForge.Server/ with git mv
   └── NpcForge.Tests     xUnit, free; adds CoreReferenceTests, CharacterStoreTests and InMemoryCharacterStore
   ```

   - **`NpcForge.Core`** exists so the console and the website call one set of dice, one writer and one
     set of save rules in-process, with the compiler keeping vendors, MCP, screens and the save file out.
   - **`NpcForge.Console`** exists because a GM at a terminal is one front end, and what only it needs
     lives with it.
   - **`NpcForge.Tests`** stays one project, because the `Category` trait already splits fast from slow
     and a second project would split `ConsoleProcess`.

   Where code goes, as one rule: **a rule both front ends need goes in the core; anything that touches
   the outside world goes in the front end that uses it.** Inside the core, folders follow the domain
   (`Brief`, `Interview`, `Writing`, `Loop`, `Saves`), not layers. The namespace stays `NpcForge`
   throughout, so a moved type needs no new `using`.

2. **Two ports.** A port is an interface the core declares because what is behind it lives outside the
   process.

   ```csharp
   // Port 1, unchanged: IChatClient (Microsoft.Extensions.AI). The model.

   // Port 2, new. One person's saved characters. A store instance belongs to one GM: the console's file
   // is the owner's, and the website builds one per logged-in friend, so nothing in the core names an owner.
   public interface ICharacterStore
   {
       // Keeps the character under the next number: one past the highest this store holds, 1 when empty.
       Task<int> SaveAsync(SavedCharacter character, CancellationToken ct);

       // The character saved under this number, its brief exactly as saved; null when none was.
       Task<SavedCharacter?> LoadAsync(int number, CancellationToken ct);

       // Every save's number, name, occupation and setting, in number order. Never the text.
       Task<IReadOnlyList<ListedCharacter>> ListAsync(CancellationToken ct);
   }

   // Today's shapes, moved from Storage.cs and CharacterTools.cs. The brief stays the JSON it was saved
   // as, so an old save never gains a field (ADR-009).
   public sealed record SavedCharacter(JsonElement Brief, CharacterName? Name, string Text);
   public sealed record CharacterName(IReadOnlyList<string> Syllables, string Family);
   public sealed record ListedCharacter(int Number, CharacterName? Name, string? Occupation, string? Setting)
   {
       // Reads the two keys from the saved brief by name, never through CharacterBrief. Every store
       // builds its rows here, so the rule is written once.
       public static ListedCharacter Of(int number, SavedCharacter saved);
   }
   ```

   - **`IChatClient`** is a port because the core must not know which vendor answers, and the record,
     replay and spending-cap clients already wrap it.
   - **`ICharacterStore`** is a port because saves move from a JSON file to a database in epic 6 without
     the core changing, and the core may not touch the file.

   Async from the start, because a database is, and changing a port from sync to async later reaches
   every caller. The store numbers the saves, because a database numbers its own rows; the shared
   tests hold the rule.

3. **The rest of the core's surface is concrete.** What the front ends call:

   ```csharp
   // One set of dice per run. A seed gives a seeded Random; null gives Random.Shared.
   public sealed class Dice(int? seed)
   {
       // roll_character's fields, from the roll's [Description] parameters through AIFunctionFactory,
       // as the MCP SDK derived them. The interview, ToneFiles and SavedView read this.
       public static IReadOnlyList<RollField> Fields { get; }

       // Binds the arguments by name, refuses a blank and an undefined enum, casts every die in today's
       // order. Throws RollRefusedException with the reason.
       public CharacterBrief Roll(IReadOnlyDictionary<string, object?> arguments);
   }

   public static class BriefJson { public static string Of(CharacterBrief brief); /* + WithField, Without */ }
   public sealed record Ask(Dictionary<string, object?> RollArguments, string? Scene, string Provider);
   public sealed record Draft(CharacterBrief Brief, Written Written) { public string BriefJson { get; } }

   public static class CharacterRun
   {
       public static Task<Draft> RollAndWriteAsync(IChatClient client, ChatOptions options, Dice dice, Ask ask, CancellationToken ct);
       public static Task<int> SaveAsync(ICharacterStore store, Draft draft, CancellationToken ct);
   }

   public static class Archetypes { public static AIFunction Lookup { get; } }   // the writer's one tool
   ```

   `ChatAgent`, `Namer`, `SceneInterview`, `AgentLoop`, `SpendingCap`, `ToneFiles`, `NotGivenFiles` and
   `WriterInput` keep their signatures. `Draft` holds the typed `CharacterBrief`, so a save cannot carry
   a brief missing a field: the check the server made by binding JSON becomes a type, and the brief's
   text is derived from it once.

4. **Not ports:**
   - **Dice.** `Dice` wraps a `Random`. Seeded and unseeded are both a `Random`, so an `IDice` would
     have one implementation. It is an instance, not a static, because one per run is today's real
     behaviour and a website must not share one across requests. `NPCFORGE_SEED` goes; `--seed` reaches
     `Dice` directly.
   - **Traces.** The core keeps writing `[app]`, `[skill]`, `[loop]` and `[tool]` lines to
     `Console.Error`, byte for byte. A `TextWriter` or `ILogger` parameter would change every signature a
     test calls. Epic 6 may move them to `ILogger` in one sweep.
   - **The GM's dialogue.** `TextReader` and `TextWriter`, as today.
   - **Skills.** Product text, read through one door (decision 6).
   - **Tools.** `IToolSource` moves into the core unchanged as the loop's seam, with `ConsoleTool` and
     the tests' `FakeToolSource`. Nothing behind it is outside the process after the switch.
   - **The clock.** Nothing in the core reads the time.

5. **The MCP server retires** in the behavior PR, with `McpToolSource`, `AppOnly`, the `ModelContextProtocol`
   package and the `[server]` trace lines. The roll, save, load and list become plain core methods,
   never an `AIFunction` in a tool list, so the model cannot see them by construction instead of by a
   filter. The roll's own `AIFunction` is private inside `Dice`, used only for its schema and its
   binding. The writer's tools are `new ConsoleTool(Archetypes.Lookup)`, chosen inside `CharacterRun`.

6. **Skills move to `NpcForge.Core/skills/`** as content, so every front end gets them in its bin at
   today's `skills\...` path. Every runtime path, every `[skill]` trace line, every fingerprint and
   every test path stays as it is. The core reads them only through `SkillFiles` (`Read`, `Exists`), the
   one file allowed to suppress the banned-file rule, with a why-comment. The lab's skill folders go
   through the same door. `ToneFiles.Require` still runs in `Program.cs` before the interview and before
   any model call. The owner accepted this one exception to "no file IO" on 2026-11-08; passing skill
   text in as data would edit about ten tests, which a restructure PR may not.

7. **The reference test.** `CoreReferenceTests`, a fast test with no `Process` trait, reads
   `NpcForge.Core.csproj` and fails unless every `PackageReference`, `ProjectReference`, `Reference` and
   `FrameworkReference` is on an allow-list of two: `Microsoft.Extensions.AI.Abstractions` and
   `Microsoft.CodeAnalysis.BannedApiAnalyzers`. So a reference to the console, a web project, OpenAI,
   Anthropic, `ModelContextProtocol` or `Microsoft.AspNetCore.App` fails it, used or not. It also fails
   if the banned-API rule (`RS0030`) is suppressed in any file but `SkillFiles.cs`. "No file IO" is the
   compiler's job: `BannedSymbols.txt` lists `System.IO.File`, `Directory`, `FileInfo`, `DirectoryInfo`,
   `FileStream`, the path constructors of `StreamReader` and `StreamWriter`, `HttpClient`,
   `Environment.GetEnvironmentVariable`, and `Console.In`, `Console.Out`, `Console.Write*` and
   `Console.Read*`, with `RS0030` an error. `Console.Error` stays allowed for traces. The test reads the
   project file, not `GetReferencedAssemblies()`, because the compiler drops an unused reference from
   the assembly, so the Done-when check (add a reference, see it fail) would pass for the wrong reason.

8. **AGENTS.md, reworded.**

   "Two processes, no shared contracts project" in "Rules that must not break" becomes:

   > **One core, called in-process.** `NpcForge.Core` holds every rule the front ends share: the dice,
   > the brief, the namer, the writer, the interview and the save rules. The console calls it
   > in-process, and so will the website. The core references only .NET and
   > `Microsoft.Extensions.AI.Abstractions`: no front end, no model vendor, no MCP, and it reads no file
   > but the bundled skills, through `SkillFiles`. `CoreReferenceTests` and the core's
   > `BannedSymbols.txt` hold this. Widening either needs an ADR.

   "No Clean Architecture layering. ..." in "Code" becomes:

   > **One core, front ends around it, no further layers.** A rule both front ends need goes in
   > `NpcForge.Core`; anything that touches the outside world goes in the front end that uses it
   > (`NpcForge.Console`, later `NpcForge.Api`). No Domain and Application split, no use-case handlers,
   > no mediator. Inside the core, folders follow the domain: `Brief`, `Interview`, `Writing`, `Loop`,
   > `Saves`. ADR-014.

   "No abstraction until there are two things to abstract." in "Code" becomes:

   > No abstraction until there are two things to abstract, with one exception: a port, an interface
   > the core declares because what is behind it lives outside the process. There are two, `IChatClient`
   > (the model) and `ICharacterStore` (one person's saves), and each new one needs an ADR and a second
   > implementation that passes the same tests. Dice, the clock, traces and skill text are not ports.

   These three change in this record's PR, because the restructure PRs already share the core between
   the console and the server. Three more lines change in the behavior PR, when they become true.
   `roll_character` "is never shown to the model ...
   (`McpToolSource.AppOnly`)" becomes "Roll, save, load and list are plain core methods, never an
   `AIFunction` in a tool list. The writer's one tool is `Archetypes.Lookup`." "The server never writes
   to stdout" becomes "The core never writes to stdout or reads stdin; the banned list holds it." The
   path in "My saves" becomes `NpcForge.Console/characters.json`.

9. **The order of PRs.** Restructure first, behavior last, as ROADMAP.md asks:
   1. Restructure. `NpcForge.Core`, the reference test and the banned list. The brief, the enums,
      `Tables`, `Looks` and the roll (`Dice`) move in. The server's `CharacterTools` stays as the MCP
      adapter: every member a test names is kept, and each body is one call into the core.
   2. Restructure. `ICharacterStore` and its records move in. `FileCharacterStore` is written in the
      server project, its only caller for now. `InMemoryCharacterStore` lives in the tests, and
      `CharacterStoreTests` runs one set of save, load and list tests against both.
   3. Restructure. The loop, the namer, the writer, the interview, the spending cap and `skills/` move
      in. `McpToolSource` stays in the console and implements the core's `IToolSource`. Three equality
      tests prove the switch before it happens: `Dice.Fields` equals `RollField.ListFrom` of the server's
      schema; `Archetypes.Lookup`'s name, description and schema equal the server's `lookup_archetype`;
      and `BriefJson.Of(new Dice(s).Roll(a))` equals the server's `roll_character` text for several seeds
      and arguments.
   4. Behavior. `CharacterRun` moves in and calls the core. The console builds `Dice` and
      `FileCharacterStore`. `NpcForge.Server`, `McpToolSource` and the MCP package are deleted.
      `FileCharacterStore` moves to the console, and `characters.json` moves to
      `NpcForge.Console/characters.json` with `git mv` after any uncommitted saves are committed. The
      owner agreed on 2026-11-08 that tests which checked the server process, its traces or
      `McpToolSource` (about twelve files) may change here beyond their `using` lines, each one listed
      in the PR. Close-out.

   `CharacterRun` moves in the behavior PR, not the third as the epic's task list says, because its
   body is the switch.

   The first PR also adds the script the Done-when asks for: over a restructure PR's diff, it fails on
   any changed line in `NpcForge.Tests/` that is not a `using` line, a project reference, or a
   namespace qualifier (`DiceOrderTests` names `NpcForge.Server.CharacterTools.ReadCast` in full).
   Every restructure PR runs it and shows its output.

## Why

- **One core project, not three layers.** A Domain and Application split would add a hop to every
  trace and enforce no rule the core boundary does not (principle-subtract-before-you-add,
  principle-minimize-reader-load).
- **No Infrastructure project.** The file store, the vendor clients and record and replay each have
  one caller, the console, and keep it after epic 6 (principle-laziness-protocol). The project is added
  when a second front end needs the same adapter.
- **A store per person, not an owner parameter.** A store is one GM's shelf of saves. Built per friend,
  it numbers each friend's saves from 1 and no query in the core can forget an owner filter; an owner
  parameter would make every console call pass a constant (principle-model-the-domain,
  principle-boundary-discipline: the owner is fixed where the request comes in).
- **The brief typed after the roll.** `Dice.Roll` is where loose arguments become a `CharacterBrief`,
  and everything after it trusts the type (principle-boundary-discipline).
- **The model's tool list built in one place.** A hand-kept `AppOnly` filter is a second copy of the
  rule the list already holds (principle-model-the-domain).
- **Skills through one door.** The rule against file IO exists so saves can leave the file and the
  core can serve a website. Skills are versioned product shipped beside the core, and a database never
  holds them. One door keeps every signature and test as it is (principle-laziness-protocol), and the
  compiler still refuses a file read anywhere else (principle-boundary-discipline).
- **The MCP server retired.** It would keep a second copy of the roll's parameters in step with the
  core, for a caller that no longer exists (principle-subtract-before-you-add).
- **An allow-list on the project file.** It catches the reference nobody thought to deny, and an unused
  reference added to show the test failing still fails it.

## Left out, and why

- **Domain, Application and Infrastructure projects; MediatR, CQRS, use-case handlers.** `CharacterRun`
  is the use case, at two methods.
- **DI in the console.** The epic forbids it. Only the website gets DI, in epic 6.
- **`IDice`, `IClock`, `ILogger`, `IGmChannel`, `ISkillSource`.** One implementation each, or nothing
  outside the process behind them.
- **A `CharacterService` facade over the store.** It would pass three calls through, and would need a
  model client before `--list` and `--load`, which today need no API key.
- **An owner parameter on the store.** The instance is the owner.
- **A typed roll request instead of the argument dictionary.** The right domain move, but it rewrites
  the flag table, the interview and the lab. A follow-up.
- **Folding `IToolSource` into a list of `AIFunction`s, renaming `ConsoleTool`, and merging
  `CharacterName` with the console's `NamedCharacter`.** Each is worth doing, and each changes tests
  beyond their `using` lines. A follow-up after the epic.
- **A step-by-step interview for the web.** A browser cannot answer a `TextReader`. Epic 6 adds a
  front-end-neutral entry beside `RunAsync`; its shape is that epic's design.

## Consequences

- **Epic 6 plugs in.** It adds `NpcForge.Api` (ASP.NET Core; references the core, gets DI and logins),
  a React app beside it that calls the API over HTTP, and a database store in the API that passes
  `CharacterStoreTests`, plus one test that two friends' stores never see each other's saves.
  Ownership is a scoped registration:
  `services.AddScoped<ICharacterStore>(sp => new DbCharacterStore(db, CurrentFriendId(sp)))`. Each
  friend gets a `SpendingCap`. Two pieces of the core change then, both additive and both that epic's
  design: the interview gains an entry that takes one answer and returns the next question, because
  each answer in the browser is its own request; and the run needs a way to report progress or stream
  the text, because a browser never sees `Console.Error` and the spending cap refuses streaming.
- **The terminal changes once, in the behavior PR.** The `[server]` lines go. The server's dice line
  becomes `[app] dice: seed N` or `[app] dice: random`. `lookup_archetype` gains `ConsoleTool`'s
  `[tool]` result line. *Needs the owner's eyes.*
- **The model must be sent the same bytes.** The interview's fields and `lookup_archetype`'s schema
  come from `AIFunctionFactory` in-process. The core references Microsoft.Extensions.AI.Abstractions
  10.8.3, the version the server resolves, so the restructure PRs do not lift the server's schema
  writer; the console runs 10.10.0, as it does now. The third PR's equality tests show any difference
  before the switch. If the switch changes what the model is sent, the work stops and asks.
- **Traces stay on `Console.Error`.** On a website they mix across requests in the host's log until
  epic 6 moves them.
- **`ChatOptions` is changed by `AgentLoop`** (it sets `Tools`), so the website builds options per
  request, never one for all.
- **Superseded:** ADR-012 and the INDEX lines on starting the server and on traces. Lessons 012, 014
  and 015 describe a server that no longer starts; they stay as history.
- **Docs that name old paths change in the behavior PR:** AGENTS.md, `docs/architecture.md`, README,
  the verify skill and `docs/runs/README.md`. Older run records keep their paths as history.
