# ADR-005: Whole runs are regression-tested by replaying a recorded model

- **Status:** accepted
- **Date:** 2026-10-01
- **Supersedes:** none
- **Plan version:** v04

## Context
Steps 10 and 12 each restructure a whole process. Before step 9, the only proof that a run still
behaved was a paid run judged by eye. The dice were `Random.Shared`, so no two runs shared a brief.
The only record of what the model was sent was a console paste, which is not byte-exact (lesson
006) and comes out in the wrong order when redirected (lesson 005).

## Options considered
- **A** Paid runs judged by eye, as before: money on every refactor, and "looks the same" is an
  impression, not a result.
- **B** A scripted fake model with a golden file of the app's requests: free and survives
  refactoring just as well, but it is not a real provider's response shape, and it is no transcript.
- **C** Record one real run with seeded dice, every request and response, to a JSONL file. Replay it
  through a `replay` provider that stops the run when the app's request differs from the recording.

## Decision
C. In detail:
- **Seed.** The server reads `NPCFORGE_SEED` from its environment. The console takes `--seed` and
  passes it through `McpToolSource`, the same way as the save path. It is never a `roll_character`
  parameter: that tool's parameters are exactly the brief fields a DM may set, and step 13 builds its
  interview from them.
- **Recording.** `--record <file>` wraps the real client in `RecordingChatClient : DelegatingChatClient`.
  Line 1 holds the run's answers as flags: the values actually used, without `--provider` or
  `--record`. `--record` needs `--seed`, because without one a recording could never replay.
- **What a replay compares.** The messages the app wrote (system, user, tool results), not the
  model's own messages. Also the model id, the output cap, and each tool's name, description and
  schema. The library never serialises `ChatOptions.Tools`, so the recording writes them by hand.
  Line endings are normalised.
- **The test.** It runs the console as a process (a black box that survives step 12) and checks the
  save file, not stdout. The save path reaches the server by inheritance from the console's
  environment. The expected brief and text come from the fixture itself.
- **Re-recording.** One paid command, kept in `.claude/rules/team.md`. It is expected after a
  deliberate behaviour change, only in a feature step, once the change has settled, and in the same
  commit. Never in a refactor step: a replay failure there is the bug the net exists to catch.
- **Runs.** The transcript sits beside the console paste in `docs/runs/`, not instead of it. It
  holds the model's side byte for byte but none of the stderr trace.

## Why
The two refactor steps need "behaviour unchanged" as a test result, for free. Option B would give
that too. C was chosen because the responses are a real provider's, and because the same file is
the first exact transcript of what the model was sent and what it said. Lesson 006 showed a paste
cannot be that transcript. Keeping the seed out of `roll_character` protects step 13's rule that
every parameter of that tool is something a DM may set.

## Consequences
- Easier: refactor steps prove behaviour unchanged with no paid run; the whole app can be run under
  the debugger against the recording for free; any later step can check it did not change what the
  model is sent.
- Harder: any deliberate change to what the model is sent (a skill edit, a tool description) turns
  the replay red until it is re-recorded, which is paid. While a skill is being tuned, the test is
  red on purpose.
- Revisit if: a .NET major upgrade changes the sequence `new Random(seed)` gives (re-record); a real
  provider's reply stops round-tripping through JSON; or re-recording becomes frequent enough to cost
  real money.
