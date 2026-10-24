# Roll and write a character, replayed

A run rolls a character brief with seeded dice on the server, hands it to the model, lets the model call `lookup_archetype`, saves what the model writes as number 1, and prints it. The model here is the recorded run, so the whole app runs for free. This is the free stand-in for the paid main path.

## Sub-features

- `replay-run` rolls the seeded brief, makes two model turns with one tool call between them, and saves as 1.
- `replay-order` writes the trace in the order the app ran: brief, tool call, second turn, save.
- `replay-streams` keeps the answer on stdout and every trace line on stderr.
- `replay-save` writes the brief and the text to the temp save file and leaves the tracked `characters.json` alone.
- `replay-seed` rolls the same brief and saves a byte-identical file on every run with the same seed.
- `replay-drift` stops at the first request line that differs from the recording and saves nothing.

## How to get to it (user POV)

- Run `dotnet run --project NpcForge.Console` with the answers as flags. This is the paid path and this skill never drives it.
- Run it with `--provider replay --recording NpcForge.Tests/Fixtures/replay.jsonl` and the flags on line 1 of that file. This is the free path.
- Run `$V replay NAME`, which is the free path behind the three fences in `../SKILL.md`.
- Run `dotnet test`, which replays the same file through `ReplayTests`. See [Free test suite](./test-suite.md).

## Driving it with npcforge-verify

Preconditions:

- `$V doctor` prints no `FAIL`.
- `$D` is a new directory with no `save.json` in it.
- The fixture has at least two model calls and one assistant tool call. `ReplayTests` enforces this.

- **Replay with split streams.** Run `$V replay --split replay`. Exit code `0`. The helper prints `[app] replay <model>`, the `[app] brief` line and `[app] saved as 1`, then `save.json: present`.
- **Replay with the order kept.** Run `$V replay --save order.json replay-ordered`. Exit code `0`, and `$D/replay-ordered.txt` holds stdout and stderr in the order written.
- **Check the order.** Run `grep -nE '^\[app\] brief|^\[tool\] lookup_archetype|^\[loop\] turn 2|^\[app\] saved as' "$D/replay-ordered.txt" | cut -c1-40`. Four lines print, with ascending line numbers, in that order. The roll comes before the model's first turn.
- **Check the streams.** Run `grep -cE '^\[(app|server|skill|loop|tool)\]' "$D/replay.stdout"` and expect `0`. Run `grep -c '^\[app\] saved as 1' "$D/replay.stderr"` and expect `1`. Stdout holds the character and nothing else.
- **Check the save.** Run `grep -c '^  "1": {' "$D/save.json"` and expect `1`. Run `grep -cE '^    "(brief|text)":' "$D/save.json"` and expect `2`. Run `git status --short -- NpcForge.Server/characters.json` and expect no output.
- **Check the seed.** Run `cmp "$D/save.json" "$D/order.json"` and expect no output. Two runs with one seed saved identical files.
- **Check the drift stop.** Run `$V replay --save drift.json drift -- --seed 2`. The helper exits non-zero by design. Run `grep -c 'differs from the recording at line' "$D/drift.txt"` and expect `1`. Run `grep -c '^\[app\] brief' "$D/drift.txt"` and expect `1`, which shows the roll ran and the stop came at the model call. Run `ls "$D/drift.json"` and expect that it does not exist.
- **Proof.** `$D` holds `replay.cmd`, `replay.stdout`, `replay.stderr`, `replay-ordered.txt`, `drift.txt`, `save.json`, `order.json` and `index.tsv`. The `index.tsv` lines for `replay` and `replay-ordered` read `save.json present` and `order.json present`, and the one for `drift` reads `drift.json absent`.

## Gotchas

- A second replay into the same save file saves as 2, and `load-saved` expects entry 1. Give every extra replay its own `--save`.
- The replay compares what the app sends with the recording and stops at the first line that differs. After an edit to the npc-writer `SKILL.md`, a tool description or schema, a `ChatOptions` field or `Tables.cs`, a stop is drift and not a flaky run. Report it. A re-record is paid and needs the owner's yes.
- The dice must roll the recorded brief, so the run needs the fixture's `--seed`. A run without `--seed` rolls random dice and drifts at the first model call.
- The `[server]` lines come from another thread, so do not assert on how a `[server]` line sits next to an `[app]` line.
- The `[skill]` line names the `SKILL.md` the model was given by its length and a short hash. Record it with the evidence when the task changed that file.
- This path never exercises the OpenAI or Anthropic client. A change to `ModelClients.cs` is covered only by a paid run, which needs the owner's yes.
