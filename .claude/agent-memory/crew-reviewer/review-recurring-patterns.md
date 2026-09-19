---
name: review-recurring-patterns
description: Mistakes that keep coming back in NpcForge step work; check for them first when a step is finished
metadata:
  type: project
---

- **Comments that name a future step go stale when that step lands.** For example, the header of `Program.cs` still said "Step 5 adds connect to the server" after step 5 was in. When a step finishes, grep the changed files for "Step N" and check the tense.
- **Doc lines and comments that count things drift.** AGENTS.md "Build and run" describes the tests by number ("three tests of `AgentLoop`"); step 5 missed the `# free:` comment, step 6 added a fourth test and missed the paragraph. Code comments too: `Program.cs` said "the three answers" after `--occupation` made four. Grep changed files and AGENTS.md for number words.
- **Why-comments in XML project files and CLI flags.** A `--` inside a csproj comment breaks loading (lesson 003). Grep `*.csproj`/`*.slnx` for `--` whenever a comment is added. The only matches should be `<!--` and `-->`.
- **Comments cut off mid-sentence.** Step 6 left `CharacterTools.cs` with "an easy character can" and three blank lines under it. Read every new comment to its full stop.
- **Watch at step 8:** `McpToolSource.InvokeAsync` looks the tool up in the unfiltered `_tools`, so `AppOnly` hides tools from the model's list but not from its door. Flagged as a warning at step 6; at step 8 `save_character`/`load_character` join `AppOnly`, which raises the stakes. Check whether it was fixed.

**Why:** the user writes the code and the comments by hand from sketches, and treats comments as notes to read in six months. That makes stale comments a real cost here, not a nitpick.

**How to apply:** check all of these on every step-finish review. Related: [[review-false-positives]].
