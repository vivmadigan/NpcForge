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
- [007 A launch profile puts a line on stdout above the answer](007-launch-profile-stdout-line.md) — applies to: `**/Properties/launchSettings.json`, NpcForge.Tests/ReplayTests.cs, docs/runs/**
- [008 A test that starts the app with `dotnet run --no-build` and no `-c` runs the Debug build](008-dotnet-run-no-build-runs-debug.md) — applies to: NpcForge.Tests/ConsoleProcess.cs, NpcForge.Console/McpToolSource.cs
- [009 A writer told "the brief wins" and "every lever names its brief entry" writes the opposite of the scene](009-brief-wins-inverts-the-scene.md) — applies to: NpcForge.Console/skills/npc-writer/SKILL.md, NpcForge.Console/ChatAgent.cs
- [010 A tone file that only nudges the voice gives six characters that read the same](010-tone-file-that-nudges-the-voice.md) — applies to: NpcForge.Console/skills/npc-writer/tones/*.md, NpcForge.Console/skills/npc-writer/SKILL.md
- [011 A PR merged before its last push leaves master without its re-record](011-pr-merged-before-its-rerecord.md) — applies to: NpcForge.Tests/Fixtures/replay.jsonl, NpcForge.Server/characters.json, PRs that need a paid re-record
