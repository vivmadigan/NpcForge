# Scene interview

With no answer flag (`--setting`, `--want`, `--difficulty`, `--occupation`) and no `--load`, the console greets the GM on stderr and waits for a scene typed in one line. A model records what the scene says, the console shows every field with its label and the GM's words, and it rolls only after the GM types `yes`. Anything else, end of input included, stops without rolling, writing or saving. Only the end-of-input path is free to drive here: a scene reaches a model.

## Sub-features

- `interview-greets` prints the greeting and a `> ` prompt on stderr when no answer flag is given.
- `interview-eof` stops at end of input with `[app] not rolled: the GM did not say yes`, exit `0`, no model turn and no save.
- `interview-skipped` does not run when any answer flag or `--load` is given. Every other recipe in this map passes one, so none of them sees the greeting.
- `interview-record-refused` refuses `--record` with no answer flag. It is covered by `RunOptionsTests.Record_with_no_answer_flags_is_refused` only: the helper's fences put an answer flag or `--provider replay` on every run, and either stops the run before this refusal.

## How to get to it (user POV)

- Run `dotnet run --project NpcForge.Console` with no flags, type the scene, then type `yes`. This is a paid run and is never driven here.
- Run `$V run NAME -- --provider replay --recording NpcForge.Tests/Fixtures/replay.jsonl < /dev/null`, which reaches the interview with no stdin and stops there for free.

## Driving it with npcforge-verify

Preconditions:

- `$V doctor` prints no `FAIL`, including `scene-interviewer SKILL.md in bin matches the source`.

- **End of input.** Run `$V run --save interview.json interview-eof -- --provider replay --recording NpcForge.Tests/Fixtures/replay.jsonl < /dev/null`. Exit code `0`.
- **Greeting and stop.** Run `grep -c 'Describe the scene in one go' "$D/interview-eof.txt"` and expect `1`. Run `grep -c '\[app\] not rolled: the GM did not say yes' "$D/interview-eof.txt"` and expect `1`.
- **Nothing reached a model, the dice or the file.** Run `grep -cE '^\[(loop|skill|tool)\]|\[app\] brief' "$D/interview-eof.txt"` and expect `0`. Run `grep interview-eof "$D/index.tsv"` and expect `interview.json absent`. The positive control is the `tools/list` `[server]` line above the greeting: the run connected and stopped at the interview, not before it.
- **Skipped with an answer flag.** `replay-run` passes the fixture's answer flags. Run `grep -c 'Describe the scene' "$D/replay.txt"` (or `replay.stderr` after `--split`) and expect `0`.
- **Proof.** `$D` holds `interview-eof.cmd`, `interview-eof.txt`, `interview-eof.exit` and `index.tsv`.

## Gotchas

- Always redirect stdin from `/dev/null` for this recipe. Without it the console waits for a GM until the helper's watchdog ends it (exit `124`).
- Piping a scene in with `--provider replay` sends the interview's request to a recording made from flags, so the replay stops at model call 1 with `differs from the recording`. That is free and proves nothing about the interview. The free proof of a scene, the confirmation and `yes` is `SceneInterviewTests`, which uses a scripted stand-in model.
- Whether a real model quotes the GM exactly, and how the confirmation reads, is *needs my eyes* on a paid run.
