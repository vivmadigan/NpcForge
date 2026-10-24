# Flag refusals

The console refuses a bad flag combination before it connects to the server or calls a model, with an unhandled `ArgumentException` on stderr. A difficulty the roll does not know stops the run at the roll, before the model's first turn. This is the cheapest check that a change to flag handling did not open a paid path.

## Sub-features

- `guard-record-nofile` stops `--record` given last with no file.
- `guard-recording-noreplay` stops `--recording <file>` without `--provider replay`.
- `guard-record-replay` stops `--record <file>` together with `--provider replay`.
- `guard-record-noseed` stops `--record <file>` without `--seed`.
- `guard-load-nonum` stops `--load` given last with no number.
- `guard-replay-norecording` stops `--provider replay` with no `--recording`.
- `guard-roll-first` stops a difficulty the roll does not know, before any model turn.

## How to get to it (user POV)

- Type the flags after `dotnet run --project NpcForge.Console --`.
- Run `$V run NAME -- <flags>`, which adds the three fences in `../SKILL.md`.

## Driving it with npcforge-verify

Preconditions:

- `$V doctor` prints no `FAIL`.
- The machine has an `OpenAI:ApiKey` user secret, for `guard-roll-first` only. Without one the run stops earlier at `Missing user secret`, which is still free but does not reach the roll. Report that as an unmet precondition.
- `N="--difficulty Nonsense"` is set in the call. It goes first in every flag list below.

- **No file.** Run `$V run --save guard.json g-record-nofile -- $N --record`. The message contains `--record needs a file`.
- **Recording without replay.** Run `$V run --save guard.json g-recording-noreplay -- $N --recording "$D/never-read.jsonl"`. The message contains `--recording only goes with --provider replay`.
- **Record with replay.** Run `$V run --save guard.json g-record-replay -- $N --seed 1 --provider replay --record "$D/never-written.jsonl"`. The message contains `--record cannot go with --provider replay`.
- **Record without a seed.** Run `$V run --save guard.json g-record-noseed -- $N --record "$D/never-written.jsonl"`. The message contains `--record needs --seed`.
- **Load without a number.** Run `$V run --save guard.json g-load-nonum -- $N --provider replay --recording NpcForge.Tests/Fixtures/replay.jsonl --load`. The message contains `--load needs a number`.
- **Replay without a recording.** Run `$V run --save guard.json g-replay-norecording -- $N --provider replay`. The message contains `--provider replay needs --recording`.
- **Roll first.** Run `$V run --save guard.json g-roll-first -- $N`. The message contains `roll_character failed`, and a `[server]` line names `NpcForge.Server.Difficulty`.
- **Check each refusal.** Define `chk() { grep -qF -- "$2" "$D/$1.txt" && ! grep -qE '^\[(loop|skill|tool)\]' "$D/$1.txt" && echo "ok   $1" || echo "FAIL $1"; }` and run `chk g-record-nofile '--record needs a file'`, then the same for each run and message above. Every line prints `ok`. The second test shows no model turn happened.
- **Check nothing was written.** Run `ls "$D" | grep -E 'never-|guard.json'` and expect no output. Run `grep -c 'guard.json absent' "$D/index.tsv"` and expect `7`, one for each refusal run. Each of those runs reached its refusal, and none reached a save.
- **Proof.** `$D` holds a `.cmd`, `.txt` and `.exit` for each `g-*` run, and `index.tsv`.

## Gotchas

- Put `--difficulty Nonsense` first. If the flag under test sits before it, a refusal that wants a value reads `--difficulty` as that value. `--record --difficulty Nonsense` fails with `--record needs --seed`, which hides the `--record` with no file refusal.
- Every refusal exits with the same code (`127` in Git Bash, `-532462766` in PowerShell). The message is the proof, so use `chk` and not the exit code.
- The checks fire in the order of `Program.cs`. `--record` without `--seed` fires before the others, so `guard-record-replay` needs `--seed 1` to reach its own message, and `guard-recording-noreplay` must not pass `--record`.
- Give `--record` a path inside `$D`. The helper refuses any other, because the error message suggests the fixture and a broken guard plus replay would overwrite it.
- `guard-roll-first` runs with the default OpenAI provider, which the helper allows only because `--difficulty Nonsense` is first. If the roll ever stopped rejecting `Nonsense`, the dead proxy is the second fence. Stop and report it. Do not rerun it.
- A refusal that exits `0`, or that prints a `[loop]`, `[skill]` or `[tool]` line, means the guard no longer stops before the model. That is a failure of the feature.
