---
name: review-recurring-patterns
description: Mistakes that keep coming back in NpcForge step work; check for them first when a step is finished
metadata:
  type: project
---

- **Comments go stale when the thing they explain moves.** Two shapes. (a) A comment names a future step, e.g. `Program.cs` "Step 5 adds connect to the server" after step 5 was in. (b) A comment's *reason* belonged to a choice that was later superseded inside the same step, e.g. step 8's `Storage.Save` "The folder does not exist before the first save", written for `%LOCALAPPDATA%\NpcForge` and left after the file moved to the always-existing server project folder. When a step records a "moved"/"first X, then Y" in PLAN.md, `git show` the first commit of the file and re-read every comment against the final choice.
- **Doc lines and comments that count things drift.** AGENTS.md "Build and run" still says "three tests of `AgentLoop`" (eight tests at step 8, five of them `McpToolSourceTests`). `Program.cs` flags comment "and the three answers" has survived steps 6, 7 and 8 (four answers plus `--load`). PLAN.md prose too: step 8 "answer all four" after listing three. Grep changed files, AGENTS.md and the step's PLAN section for number words.
- **Why-comments in XML project files and CLI flags.** A `--` inside a csproj comment breaks loading (lesson 003). Grep `*.csproj`/`*.slnx` for `--` whenever a comment is added. The only matches should be `<!--` and `-->`.
- **Comments cut off mid-sentence.** Step 6 left `CharacterTools.cs` with "an easy character can" (fixed at step 8). Read every new comment to its full stop.
- **Silent default changes.** Step 8 changed `Program.cs`'s default model (`gpt-5.6-terra` -> `gpt-6-luna`) with no line in PLAN.md. Diff the defaults block of `Program.cs` every step; it decides what a bare paid run costs and which vendor family the evidence comes from.
- **AppOnly door (resolved).** Fixed at step 7 (`ModelTools` feeds both `ListAsync` and `InvokeAsync`); verified again at step 8 by removing the filter in a scratch copy. See [[review-environment]] for the technique.

**Why:** the user writes the code and the comments by hand from sketches, and treats comments as notes to read in six months. That makes stale comments a real cost here, not a nitpick.

**How to apply:** check all of these on every step-finish review. Related: [[review-false-positives]].
