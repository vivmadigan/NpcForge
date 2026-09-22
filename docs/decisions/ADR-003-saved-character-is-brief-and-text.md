# ADR-003: A saved character is the brief and the written text together, loaded by number

- **Status:** accepted
- **Date:** 2026-09-22
- **Supersedes:** none
- **Plan version:** v02

## Context
Step 8's original plan followed BUILD.md's save format literally: persist the brief and reproduce
the character from it, loaded by name. The challenger reviewed that plan before any code was
typed (WEAKENED, 2026-09-22) and found four problems: the brief has no name and the model writes
differently every run, so reloading it gives the same traits but a new name, new levers and a new
"Afterwards" — it fails README's success test ("load the second one back unchanged") and README
build order 4, which says to store the character "along with the brief that produced them"; nothing
in the plan ever called `save_character`; a mistyped name would reach the model as an empty brief;
and tests would share the user's own save file.

## Options considered
- **A** The brief only, saved by name, as BUILD.md sketches it: fails the reload test above, and the
  plan never actually called the save tool.
- **B** Settle the name in the brief, the way ADR-002 settled occupation: fixes the name, but every
  load still produces new writing, since the levers, the "Afterwards" and everything else the model
  wrote are still regenerated, not stored.
- **C** Save the brief and the written text together, one file, loaded by number.

## Decision
C. Every run saves automatically — the user's call, "for the moment": choosing what to keep belongs
with the interactive console that comes after the finish line, not this step. `save_character` and
`load_character` are app-only server tools, in `NpcForge.Server/characters.json`, with
`NPCFORGE_CHARACTERS` overriding the path so tests use their own temp files.

## Why
The character is the written text, not the brief that produced it; saving only the brief was never
going to satisfy "load the second one back unchanged" no matter which field is missing from it,
because the model's prose is not reproducible from the brief alone. Loading by number rather than
name is a consequence of the same fact: the name lives inside the model's free text, and step 7
found its formatting differs by model, so parsing a name out of it would break on the next vendor.
A number needs no parsing and is stable across every model. The file moved from
`%LOCALAPPDATA%\NpcForge` to `NpcForge.Server`'s own project folder the same day, because the user
could not find it there; it is still found from `AppContext.BaseDirectory`, never a relative path,
since the server's working folder is whatever the app was started from (step 7).

## Consequences
- Easier: a load is free and byte-exact, proven by a free round-trip test; a real database later
  replaces `Storage.cs` and nothing else, because the app only ever calls `save_character` and
  `load_character`.
- Harder: this departs from README build order 4 and from BUILD.md step 8 ("load it by name"); the
  file grows with every paid run, not only the ones worth keeping, until the interactive console
  gives the user a way to choose.
- Revisit if: the interactive console arrives (naming a save, choosing what to keep), the file grows
  unwieldy, or a real database is wanted.
