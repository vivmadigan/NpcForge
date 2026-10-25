# NpcForge verification map

This directory is the maintained source for verifying the user-facing behavior of NpcForge. Read the index before driving the app, then use the matching feature file as the recipe. `../SKILL.md` holds the launch, doctor, evidence and cleanup rules that every recipe assumes.

## Baseline preconditions

- Run from the repo root in Git Bash. From PowerShell, call Git's `bash.exe` as `../SKILL.md` shows.
- Run `dotnet build` first, outside the helper. It must succeed with no `NpcForge.Console` or `NpcForge.Server` process running.
- Start every Bash call with `V=.claude/skills/verify/scripts/npcforge-verify.sh; D=$($V dir)`. `V` is the helper and `D` is the evidence directory.
- Run `$V start` once, then `$V doctor`, and require no `FAIL` line.
- The answers for a replay are line 1 of `NpcForge.Tests/Fixtures/replay.jsonl`. The helper reads them on every run, so a re-record changes nothing here.
- Never drive a process that this run did not start. Never run the console except through `$V run`, `$V replay` or `$V load`, which keep every run free.

## Driving conventions

- Start every recipe from the baseline state unless its preconditions say otherwise. Run `replay-run` before `load-saved`, which reads the `save.json` that `replay-run` makes.
- Treat every command as literal. Keep quoted names and flags unchanged.
- Give a run that must not touch `save.json` its own `--save` file, such as a refusal or a drift check.
- Judge a refusal by its message. Every unhandled exception exits with the same non-zero code.
- Never edit a `.cs` file to drive a feature. The break-it loop in `../SKILL.md` is the one exception, and it restores the file.

## Proof and skip reporting

- Capture the user action and the resulting state, not only the exit code.
- Console proof is the command (`NAME.cmd`), the transcript (`NAME.txt`, or `NAME.stdout` and `NAME.stderr`) and the exit code (`NAME.exit`) in `$D`.
- Mutation proof is a read-only second view of the stored value. For a save, that is `$D/save.json` and a clean `git status` on the tracked `characters.json`.
- Pair a negative check with a positive control. An absent save file means nothing unless the transcript shows the run reached the point where it would have saved.
- Record the feature ID and entry point used with every artifact.
- Report an unreachable path with the attempted command and the unmet precondition.
- Do not report a skipped entry point as verified through a different path. The paid run is skipped on purpose, and the replay is its free stand-in.

## Feature entry contract

Each feature file starts with an H1 title and one paragraph describing the user-visible behavior. It then uses exactly four H2 sections in this order.

1. `Sub-features` lists short IDs with one line for each behavior.
2. `How to get to it (user POV)` lists every user entry point.
3. `Driving it with npcforge-verify` starts with `Preconditions:` and uses labeled bullets that pair each user action with an exact command and observable result.
4. `Gotchas` lists traps that can waste or invalidate a verification run.

Keep implementation details out of the map. Name only user paths, stable handles, required state, commands, and observable proof.

## Features

- [Roll and write a character, replayed](./replay-run.md) covers the free stand-in for the main path: roll, tool call, write, save, with the answer on stdout and the trace on stderr.
- [Load a saved character](./load-saved.md) covers `--load N`, its exact output and its refusal of a number that was never saved.
- [Flag refusals](./flag-guards.md) covers the bad flag combinations that stop before any cost, and a difficulty the roll rejects before the model is called.
- [Scene interview](./scene-interview.md) covers a bare run waiting for the scene, and end of input stopping it with nothing rolled or saved.
- [Free test suite](./test-suite.md) covers `dotnet test`, a single test by name, and the break-it loop that shows a new test fail.

Not mapped. The paid run (`dotnet run --project NpcForge.Console` with no `--provider replay`), the interview with a real scene and model, `--record` for real, and the MCP server driven directly over stdio. Whether a character reads well is *needs my eyes*.
