# Runs

One file per paid run: what was asked, what came back, and what it showed. These files are the
evidence for any claim about how the model behaves. The point is comparison: the same answers run
against a changed skill or prompt, so a change can be judged instead of guessed at.

## Recording a run

1. **Commit first.** The SHA in the header only means something if the tree was clean.
2. Run the app and watch it live.
3. Select all in the console and paste it into a new file here, named `YYYY-MM-DD-NN-<slug>.md`.
4. Add a header: the model, the answers, the rolled obstacle, the `[skill]` fingerprint, the commit
   and the token counts. Most of it is on the `[app]`, `[skill]` and `[loop]` lines.
5. End with what the run showed.

`raw/` holds full pastes that a summary file here points at. Once the Safety net epic is built, a
run made with `--record` also has its transcript, kept beside the paste: it holds the model's side
byte for byte, but none of the trace.

## The replay fixture

The Safety net epic records one run, and the free replay test runs the whole app against it. This is
the scene to record, with every answer spelled out so a changed default can't change it silently.
It is paid: ask the owner first, and commit before running it.

```text
dotnet run --project NpcForge.Console -- --record NpcForge.Tests/Fixtures/replay.jsonl --seed 1 --provider openai --model gpt-6-luna --setting "an inn in a major city" --want "the name of a fence" --difficulty Wall --occupation innkeeper
```

Re-record only after a deliberate behavior change, once it has settled, in the same PR. Never
re-record to make a restructure pass: a red replay there is the bug the test exists to catch. A
feature that adds answers (tone, importance) adds their flags here.

## Why it is done this way

**Paste, don't redirect.** A PowerShell redirect reorders the answer against the stderr trace, and
not the same way twice, and the order is the information (lesson 005). `cmd /c "... > file 2>&1"`
keeps the order but shows nothing on screen while the run costs money.

**A paste is not byte-exact either.** The Visual Studio console flattens curly quotes and dashes,
and a paste drops trailing spaces (lesson 006). The save file and the `--record` transcript are the
exact records.

**Commit first, and fingerprint the skill.** Tuning a skill happens against a dirty tree, so two runs
an hour apart can share a SHA, and `git diff` between them is empty. A run file has to stand on its
own: commit so the SHA means something, and let the `[skill]` line's fingerprint name the guidance
whatever git says. A length alone is not enough, because two edits of the same length differ only in
the hash.
