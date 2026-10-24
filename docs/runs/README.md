# Runs

One file per paid run: what was asked, what came back, and what it showed. These files are the
evidence for any claim about how the model behaves. The point is comparison: the same answers run
against a changed skill or prompt, so a change can be judged instead of guessed at.

## Recording a run

1. **Commit first.** The SHA in the header only means something if the tree was clean. On a branch,
   name its PR as well: the owner squash-merges, so a branch SHA is not on `master` afterwards, but
   GitHub keeps every PR's commits.
2. Run the app and watch it live.
3. Select all in the console and paste it into a new file here, named `YYYY-MM-DD-NN-<slug>.md`.
   An agent cannot paste, so it captures in Git Bash instead, then appends the file:

   ```text
   <the run command> 2>&1 | tee <file>; echo "exit: ${PIPESTATUS[0]}"
   ```

4. Add a header: the model, the answers, the rolled obstacle, the `[skill]` fingerprint, the commit
   (and its PR, on a branch) and the token counts. Most of it is on the `[app]`, `[skill]` and
   `[loop]` lines.
5. End with what the run showed.

`raw/` holds full pastes that a summary file here points at. A run made with `--record` also has a
transcript, the JSONL file it names: it holds the model's side exactly, but none of the trace. The
replay fixture's transcript is `NpcForge.Tests/Fixtures/replay.jsonl`.

## The replay fixture

The Safety net epic records one run, and the free replay test runs the whole app against it. This is
the scene to record, with every answer spelled out so a changed default can't change it silently.
It is paid: ask the owner first, and commit before running it.

```text
dotnet run --project NpcForge.Console -- --record NpcForge.Tests/Fixtures/replay.jsonl --seed 1 --provider openai --model gpt-6-luna --setting "an inn in a major city" --want "the name of a fence" --difficulty Wall --occupation innkeeper
```

Re-record only after a deliberate behavior change, once it has settled, in the same PR. Never
re-record to make a restructure pass: a red replay there is the bug the test exists to catch. A
change to `Tables.cs` or to the order of the picks re-rolls the seeded brief, so it needs a re-record
too. A feature that adds answers (tone, importance) adds their flags here, and to the `flags` array
in `Program.cs` that writes line 1 of the recording.

An upgrade of Microsoft.Extensions.AI, the OpenAI library or the MCP library can also turn the replay
red with no change in behavior, because the recording is the library's own JSON. Run the replay test
first after one. If it is red and the upgrade is the only change, re-record in the upgrade's PR.

A re-record can come back with no tool call: the model may write straight from the brief. The replay
test refuses that recording, because a dropped model turn only shows on a later call. Ask the owner
before a second paid attempt, and never weaken the check to let a recording pass.

To replay it by hand, for free, in PowerShell. The flags are line 1 of the fixture. Point
`NPCFORGE_CHARACTERS` somewhere else first, or the replay's save lands in the tracked
`characters.json`, and clear it afterwards, or later runs in that terminal save there too.

```text
$env:NPCFORGE_CHARACTERS = "$env:TEMP\npcforge-replay.json"
dotnet run --project NpcForge.Console -- --provider replay --recording NpcForge.Tests/Fixtures/replay.jsonl --seed 1 --model gpt-6-luna --setting "an inn in a major city" --want "the name of a fence" --difficulty Wall --occupation innkeeper
Remove-Item Env:NPCFORGE_CHARACTERS
```

## Why it is done this way

**Paste, don't redirect.** A PowerShell redirect reorders the answer against the stderr trace, and
not the same way twice, and the order is the information (lesson 005). `cmd /c "... > file 2>&1"`
keeps the order but shows nothing on screen while the run costs money. Git Bash's `2>&1 | tee` keeps
the order and shows the run live: lesson 005's throwaway app, alternating the two streams five times,
came out in exact alternation in three runs of three (2026-10-25).

**A paste is not byte-exact either.** The Visual Studio console flattens curly quotes and dashes,
and a paste drops trailing spaces (lesson 006). The save file and the `--record` transcript are the
exact records.

**Commit first, and fingerprint the skill.** Tuning a skill happens against a dirty tree, so two runs
an hour apart can share a SHA, and `git diff` between them is empty. A run file has to stand on its
own: commit so the SHA means something, and let the `[skill]` line's fingerprint name the guidance
whatever git says. A length alone is not enough, because two edits of the same length differ only in
the hash.
