# Lessons

Hard-earned, project-specific knowledge: what went wrong, what did not fix it, what did, and why it works here. One line per lesson; open the file for the full account. This file is imported into every session through CLAUDE.md, so keep it to one line per lesson.

Why this exists alongside the "Seen in the debugger, worth remembering" notes in PLAN.md: those notes record what turned out to be true. They never record the dead ends. A lesson file has a mandatory "What did not work" section, and that is the part that saves the next hour.

## Curated

- [001 OpenAI rejects function tools on Chat Completions when reasoning is on](001-openai-responses-endpoint.md) — applies to: NpcForge.Console/ModelClients.cs — active
- [002 VS Code chat treats the repo as untrusted because it spells the drive c:](002-vscode-lowercase-drive-untrusted.md) — applies to: .claude/skills/crew/**, .claude/settings.json, ~/.claude.json (outside repo) — active
- [003 A csproj comment that mentions a CLI flag stops the project loading](003-csproj-comment-double-hyphen-load-failed.md) — applies to: `**/*.csproj`, `**/*.props`, `**/*.targets`, `*.slnx` — active
- [004 A test asserting on "Tool failed:" can pass for the wrong reason](004-tool-failed-prefix-hides-why.md) — applies to: NpcForge.Tests/**, NpcForge.Console/McpToolSource.cs — active
- [005 Redirecting a run to a file in PowerShell reorders the trace against the answer](005-powershell-redirect-reorders-trace.md) — applies to: docs/runs/**, NpcForge.Console/AgentLoop.cs, NpcForge.Console/Program.cs — active
- [006 A run and its --load look different in a console paste when the save is exact](006-console-paste-not-byte-exact.md) — applies to: docs/runs/**, NpcForge.Console/Program.cs, NpcForge.Server/characters.json — active

## Inbox (uncurated; run /crew:curate)

(empty)
