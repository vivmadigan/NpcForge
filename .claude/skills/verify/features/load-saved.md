# Load a saved character

`--load N` prints a saved character exactly as it was written, with no roll, no model and no cost. A number that was never saved stops the run with the server's reason and prints nothing.

## Sub-features

- `load-exact` prints the saved text byte for byte as the run that made it printed it.
- `load-brief` reports `[app] loaded N` and the saved brief on stderr, and the brief matches the one the run rolled.
- `load-unknown` stops on a number that was never saved, with the server's reason, and prints nothing.

## How to get to it (user POV)

- Run `dotnet run --project NpcForge.Console -- --load 3`. This builds the real model client first, so it needs `OpenAI:ApiKey` in user secrets. It makes no model call.
- Run `$V load N`, which adds `--provider replay` and the fixture so no key is needed and no real client can be built.

## Driving it with npcforge-verify

Preconditions:

- `replay-run` ran in this `$D`, so `save.json` holds entry 1. Confirm it with `grep -c '^  "1": {' "$D/save.json"` and expect `1`.
- `$D/replay.stdout` and `$D/replay.stderr` exist from `$V replay --split replay`.

- **Load entry 1.** Run `$V load 1`. Exit code `0`. The helper prints `[app] loaded 1` and `[app] brief {...}`, and its byte counts for stdout match `replay.stdout`.
- **Exact text.** Run `cmp "$D/replay.stdout" "$D/load-1.stdout"` and expect no output. The run that saved the character and the load that read it printed identical bytes.
- **Brief matches the roll.** Run `diff <(grep '^\[app\] brief' "$D/replay.stderr" | sed 's/^\[app\] brief //') <(grep '^\[app\] brief' "$D/load-1.stderr" | sed 's/^\[app\] brief //')` and expect no output.
- **Unknown number.** Run `$V load 99`. The helper exits non-zero by design. Run `grep -c 'No character saved as 99' "$D/load-99.stderr"` and expect a count of at least `1`. The message prints once as a `[server]` log line and once in the console's unhandled exception. Run `wc -c < "$D/load-99.stdout"` and expect `0`.
- **Nothing written.** Run `grep -c '^  "[0-9]*": {' "$D/save.json"` and expect `1`. A load never adds an entry.
- **Proof.** `$D` holds `load-1.cmd`, `load-1.stdout`, `load-1.stderr`, `load-99.stderr` and `index.tsv`, whose `load-1` line reads exit `0` and `save.json present`.

## Gotchas

- Load before any replay in a fresh `$D` reports `No character saved as 1`. That is the correct refusal for an empty save file, so report it as an unmet precondition and run `replay-run` first. Do not weaken the check.
- Plain `--load N` without `--provider replay` fails with `Missing user secret 'OpenAI:ApiKey'` on a machine with no key. The helper never runs it that way.
- A console paste of a character is not byte-exact. It flattens curly quotes and drops trailing spaces (lesson 006). Compare the `.stdout` files, never a paste.
- Printing adds one trailing line ending after the text. The replay and the load both print through the same call, so they match each other. Do not compare either with the JSON in `save.json` without unescaping it.
- `--load` with no number is a flag refusal and is covered by [Flag refusals](./flag-guards.md).
