---
name: lesson-clusters
description: How NpcForge's curated lessons group by area, and the rule-file threshold status
metadata:
  type: project
---

As of the step-5 curation (2026-09-19), `docs/lessons/` has three curated lessons, each in a
different area, none yet at the three-per-area threshold for a `.claude/rules/lessons-<area>.md`
file:

- **001** — OpenAI/Anthropic SDK quirks, scoped to `NpcForge.Console/ModelClients.cs`. Tags:
  openai, sdk, tools.
- **002** — Claude Code / VS Code plugin-loading and workspace-trust behaviour, scoped to
  `.claude/` config and `~/.claude.json` (outside the repo, not app code). Tags: claude-code,
  vscode, trust.
- **003** — MSBuild/XML project-file gotchas, scoped to `**/*.csproj`, `**/*.props`,
  `**/*.targets`, `*.slnx`. Tags: msbuild, xml, visual-studio.

**Why:** Each area only has one lesson so far, so per the crew rules a rule file would be
ceremony — the index is enough below three. Worth re-checking every curate run: step 5 (MCP
server work) is the kind of step likely to produce more `NpcForge.Server`-scoped or MCP-scoped
lessons, and tooling/trust issues (area 002) have already recurred once across two inbox entries
in project history — a second VS Code/Claude Code trust lesson would make that area worth a rule
file.

**How to apply:** When curating, group new entries against these three tags/areas first before
assuming a new area. If area 002 (claude-code/vscode/trust) or an MCP/server area reaches 3
lessons, write the corresponding `.claude/rules/lessons-<area>.md`.

## Process note: PLAN.md cross-links into the inbox

This project's step "Seen in the debugger, worth remembering" notes in `PLAN.md` sometimes link
directly to a lesson while it is still in `docs/lessons/inbox/`, e.g. step 5 had:
`- Lesson: <title> (docs/lessons/inbox/<file>.md)`. When curating an inbox entry that is
referenced this way, the pointer goes stale the moment the file is renamed into `docs/lessons/`.
Since PLAN.md is the live, freely-edited plan (not an immutable file the curate hook blocks),
fixing that one-line pointer to the new curated path is part of finishing the curation cleanly —
check PLAN.md's "Seen in the debugger" notes for the inbox filename before deleting the inbox
file. See [[curate-workflow]] if written.

## Other observations

- Inbox entries in this project so far have arrived already well-formed against
  `templates/lesson.md` (all five sections present, "What did not work" substantive). Little
  rewriting has been needed beyond relocating the file and fixing the destination filename/index
  line — the author already writes to the template.
