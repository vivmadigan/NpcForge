---
name: review-recurring-patterns
description: Mistakes that keep coming back in NpcForge step work; check for them first when a step is finished
metadata:
  type: project
---

- **Comments that name a future step go stale when that step lands.** For example, the header of `Program.cs` still said "Step 5 adds connect to the server" after step 5 was in. When a step finishes, grep the changed files for "Step N" and check the tense.
- **Doc lines that count or describe tests drift.** AGENTS.md "Build and run" has both a paragraph and a trailing `# free: fakes only` comment on the `dotnet test` line. Step 5 updated the paragraph but missed the comment.
- **Why-comments in XML project files and CLI flags.** A `--` inside a csproj comment breaks loading (lesson in docs/lessons/inbox, 2026-09-19). Grep `*.csproj`/`*.slnx` for `--` whenever a comment is added. The only matches should be `<!--` and `-->`.

**Why:** the user writes the code and the comments by hand from sketches, and treats comments as notes to read in six months. That makes stale comments a real cost here, not a nitpick.

**How to apply:** check all three on every step-finish review. Related: [[review-false-positives]].
