# Lessons

Problems in this project that took a dead end to solve: what went wrong, what did not fix it, what
did, and why it works here. One line each; open the file for the full account. This file is loaded
into every session, so keep it to one line per lesson.

A new lesson takes the next number and copies the shape of the others: Status, Date, Applies to,
then Symptom, What did not work, What worked, Why it works here, How to tell it is happening again.
"What did not work" is the part that saves the next hour. A plain fact that went right first time is
not a lesson. Older lessons mention `PLAN.md` and plan versions; those are in git at commit
`cd20b60`.

- [001 OpenAI rejects function tools on Chat Completions when reasoning is on](001-openai-responses-endpoint.md) — applies to: NpcForge.Console/ModelClients.cs
- [002 VS Code chat treats the repo as untrusted because it spells the drive c:](002-vscode-lowercase-drive-untrusted.md) — applies to: project-scope plugins, .claude/settings.json, ~/.claude.json (outside the repo)
- [003 A csproj comment that mentions a CLI flag stops the project loading](003-csproj-comment-double-hyphen-load-failed.md) — applies to: `**/*.csproj`, `**/*.props`, `**/*.targets`, `*.slnx`
- [004 A test asserting on "Tool failed:" can pass for the wrong reason](004-tool-failed-prefix-hides-why.md) — applies to: NpcForge.Tests/**, NpcForge.Console/McpToolSource.cs
- [005 Redirecting a run to a file in PowerShell reorders the trace against the answer](005-powershell-redirect-reorders-trace.md) — applies to: docs/runs/**, NpcForge.Console/AgentLoop.cs, NpcForge.Console/Program.cs
- [006 A run and its --load look different in a console paste when the save is exact](006-console-paste-not-byte-exact.md) — applies to: docs/runs/**, NpcForge.Console/Program.cs, NpcForge.Server/characters.json
