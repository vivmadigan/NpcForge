# ADR-015: A store never gives a save a number one already holds, and the guards outlive the server

- **Status:** accepted
- **Date:** 2026-11-08
- **Supersedes:** three lines of ADR-014. Decision 2's "one past the highest this store holds" as the
  promise `ICharacterStore` makes; decision 7's account of what `CoreReferenceTests` checks; and
  decision 9.4's silence on `Storage.cs` and `StorageTests`. The rest of ADR-014 stands.

## Context

The second restructure PR of epic 5 (the store port) had an independent Opus review on 2026-11-08,
which the owner asked for "so what we are doing is best for the project". It found three things the
design got wrong or left open. Each was shown by a run, not argued:

- **The numbering promise.** ADR-014 says a store keeps a save "one past the highest this store holds",
  and gives as its reason that "a database numbers its own rows". A database's own numbering never
  gives a removed number out again, so a database store would fail a test of the stated promise. The
  first draft of `CharacterStoreTests` pinned that reuse. Reuse is also a hazard once a save can be
  deleted: a friend's link to character 2 would quietly open another character.
- **The boundary test.** `CoreReferenceTests` read only the core's project file for references. It
  passed with the core switched to the web SDK, with `BannedSymbols.txt` taken out of
  `AdditionalFiles` (a file read then built cleanly), with analyzers turned off, and with a forbidden
  package added in a `Directory.Build.props`. "The compiler enforces it" did not yet hold.
- **The script and the save-file guard.** `scripts/check-restructure-tests.sh` passed two test lines
  swapped (a change in what a test checks) and failed a plain namespace move to `NpcForge.`.
  `StorageTests`, the only byte-for-byte guard on the owner's `characters.json`, calls server code
  (`Storage.Load()`, `CharacterTools.SaveCharacter`) that the behavior PR deletes, and ADR-014 said
  nothing about it.

## Decision

1. **A store keeps a new save under a number greater than every number it holds, 1 when empty, and
   never under a number a save holds.** Each store counts on its own, so a database store numbers each
   friend's saves from 1, with a unique key on owner and number. The file store does what it does
   today (one past the highest), which keeps the promise. A store that later gains a delete must not
   give a deleted number out again; that is the store's job, not the core's. `CharacterStoreTests`
   tests the promise, not the reuse.

2. **`CoreReferenceTests` checks the build, not only the project file.** It fails when the core's SDK
   is anything but `Microsoft.NET.Sdk`; when `BannedSymbols.txt` is not an `AdditionalFiles` item; when
   analyzers are turned off; and when a reference or an `RS0030` suppression appears in any MSBuild or
   analyzer-config file that applies to the core (its project file, and any `Directory.Build.props`,
   `Directory.Build.targets`, `Directory.Packages.props`, `.editorconfig` or `.globalconfig` from its
   folder up to the repo root). It stays a fast test that reads files.

3. **The script pairs lines in order, and the owner's exceptions are listed out loud.** A removed and
   an added line count as a qualifier change only inside one hunk and in order, once `NpcForge.Server.`
   and `NpcForge.Core.` read as `NpcForge.`. A test file the owner allowed to change is named with
   `--allow-changed <path>`, and the script prints it under its own heading. Nothing else is exempt. The
   store-port PR uses it once, for `CoreReferenceTests.cs`, by the owner's instruction; the behavior
   PR uses it for the server tests ADR-014 decision 9.4 lists.

4. **The save file's byte guard moves to the file store now.** A new test drives `FileCharacterStore`
   on a copy of the real saves and holds the same two rules `StorageTests` holds: a new save leaves
   every old entry byte for byte, and an old entry never gains a field. In the behavior PR, `Storage`
   folds into `FileCharacterStore` in the console, `StorageTests` goes with the server code it calls,
   and the new test is the guard. An unreadable file then throws a plain exception the console reports,
   not `McpException`.

5. **Two moves are planned, not made here.** In the third restructure PR, the brief's JSON options are
   decided once, for the save and for the writer's message (`SavedCharacter.Of` and `BriefJson.Of`
   share them). In the behavior PR, "a save with no name leaves `name` out of the file" moves from an
   attribute on the core's `SavedCharacter` to the file store's serializer options, because it is the
   file's rule, not the domain's.

## Why

- **The promise a database can keep.** The rule exists so a number always means one character. Stated
  as "greater than every number held, never a held one", it is what the file does today and what a
  database does on its own (principle-model-the-domain: the number is the save's identity).
- **A guard that checks what it claims.** An allow-list that one line in a side file defeats is not the
  compiler enforcing a boundary (principle-boundary-discipline).
- **A script that sees order.** A test whose lines were swapped checks something else; a check that
  cannot see it proves less than it says (principle-test-behavior-not-implementation).
- **The guard on the owner's saves cannot depend on code that is about to go.** Moving it now, while
  both run side by side, shows the new one catches what the old one did.

## Consequences

- Epic 6's database store passes `CharacterStoreTests` with its own numbering, and needs one more test
  of its own: two saves at the same moment never share a number.
- A database store that keeps a brief with `GetRawText()` keeps it escaped (`Gull's Rest`): valid
  JSON, and the file still writes it literally. Worth knowing before choosing a column type.
- The behavior PR's list of test changes now includes `StorageTests` (deleted, its guard kept by the
  new file-store test).
