---
name: lesson-clusters
description: How NpcForge's curated lessons cluster by area, and when a lessons-<area>.md rule is actually earned
metadata:
  type: project
---

As of the 2026-09-20 curate (lessons 001-005), `NpcForge.Console/**` is the first area to cross
the three-lesson threshold: 001 (ModelClients.cs), 004 (McpToolSource.cs, also touches
NpcForge.Tests/**), and 005 (AgentLoop.cs, Program.cs, also touches docs/runs/**). That produced
the project's first path-scoped rule, `.claude/rules/lessons-npcforge-console.md`, scoped to
`NpcForge.Console/**`, `NpcForge.Tests/**`, and `docs/runs/**` (the union of the three lessons'
"Applies to" paths).

Other areas seen so far sit below the threshold and should stay index-only until a third lesson
lands in the same folder:
- `.claude/skills/crew/**` / Claude Code trust config — only 002 (VS Code lowercase-drive trust).
- Build/project files (`**/*.csproj`, `**/*.props`, `**/*.targets`, `*.slnx`) — only 003.

**Why:** the librarian task rule is explicit — "Below three lessons, the index is enough; a rule
file for one lesson is ceremony." Don't create `lessons-<area>.md` speculatively; wait for the
third lesson to actually land in that folder.

**How to apply:** at the start of a curate run, after filing new lessons, recompute which folders
now have >=3 curated lessons touching them (union "Applies to" globs, not just the title/tags) —
new lessons can push an existing area over the line even if the new lesson's own primary subject
is different (e.g. 005 is about PowerShell redirection, but its file targets are what pushed
NpcForge.Console over three). See also [inbox-has-no-backlinks-yet](inbox-has-no-backlinks-yet.md).
