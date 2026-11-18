# Lessons

Problems in this project that took a dead end to solve: what went wrong, what did not fix it, what
did, and why it works here. One line each; open the file for the full account. This file is loaded
into every session, so keep it to one line per lesson.

A new lesson takes the next number and copies the shape of the others: Status, Date, Applies to,
then Symptom, What did not work, What worked, Why it works here, How to tell it is happening again.
"What did not work" is the part that saves the next hour. A plain fact that went right first time is
not a lesson. Older lessons mention `PLAN.md` and plan versions; those are in git at commit
`cd20b60`.

- [001 OpenAI rejects function tools on Chat Completions when reasoning is on](001-openai-responses-endpoint.md) — applies to: NpcForge.Adapters/ModelClients.cs
- [002 VS Code chat treats the repo as untrusted because it spells the drive c:](002-vscode-lowercase-drive-untrusted.md) — applies to: project-scope plugins, .claude/settings.json, ~/.claude.json (outside the repo)
- [003 A csproj comment that mentions a CLI flag stops the project loading](003-csproj-comment-double-hyphen-load-failed.md) — applies to: `**/*.csproj`, `**/*.props`, `**/*.targets`, `*.slnx`
- [004 A test asserting on "Tool failed:" can pass for the wrong reason](004-tool-failed-prefix-hides-why.md) — applies to: NpcForge.Tests/**, NpcForge.Core/Loop/ConsoleTool.cs
- [005 Redirecting a run to a file in PowerShell reorders the trace against the answer](005-powershell-redirect-reorders-trace.md) — applies to: docs/runs/**, NpcForge.Core/Loop/AgentLoop.cs, NpcForge.Console/Program.cs
- [006 A run and its --load look different in a console paste when the save is exact](006-console-paste-not-byte-exact.md) — applies to: docs/runs/**, NpcForge.Console/Program.cs, NpcForge.Console/characters.json
- [007 A launch profile puts a line on stdout above the answer](007-launch-profile-stdout-line.md) — applies to: `**/Properties/launchSettings.json`, NpcForge.Tests/ReplayTests.cs, docs/runs/**
- [008 A test that starts the app with `dotnet run --no-build` and no `-c` runs the Debug build](008-dotnet-run-no-build-runs-debug.md) — applies to: NpcForge.Tests/ConsoleProcess.cs, .claude/skills/verify/scripts/npcforge-verify.sh
- [009 A writer told "the brief wins" and "every lever names its brief entry" writes the opposite of the scene](009-brief-wins-inverts-the-scene.md) — applies to: NpcForge.Core/skills/npc-writer/SKILL.md, NpcForge.Core/Writing/ChatAgent.cs
- [010 A tone file that only nudges the voice gives six characters that read the same](010-tone-file-that-nudges-the-voice.md) — applies to: NpcForge.Core/skills/npc-writer/tones/*.md, NpcForge.Core/skills/npc-writer/SKILL.md
- [011 A PR merged before its last push leaves master without its re-record](011-pr-merged-before-its-rerecord.md) — applies to: NpcForge.Tests/Fixtures/replay.jsonl, NpcForge.Console/characters.json, PRs that need a paid re-record
- [012 A test that starts many servers at once fails one run in three](012-many-servers-at-once-outlast-the-start-up-wait.md) — history: the server retired in epic 5
- [013 An edit to a skill file leaves the build stale, and a plain `dotnet build` does not clear it](013-skill-edit-leaves-the-build-stale.md) — applies to: NpcForge.Tests/ConsoleProcess.cs, NpcForge.Core/skills/**
- [014 Every server a test started took 5 s to close, and the wait hid inside each test's time](014-every-server-close-waited-5-seconds.md) — history: the server retired in epic 5
- [015 In a long folder the host hands the app a `\\?\` folder, and a walk up with `..` finds nothing](015-long-folder-device-path.md) — applies to: NpcForge.Adapters/FileCharacterStore.cs, NpcForge.Tests/ConsoleProcess.cs, NpcForge.Tests/LongFolderTests.cs, code that walks up from AppContext.BaseDirectory
- [016 A file put back with its old time leaves the break in the build, and nothing says the build is stale](016-restored-file-keeps-old-time-stale-binary-runs.md) — applies to: break-it checks, NpcForge.Tests/ConsoleProcess.cs, any C# file restored from a copy
- [017 A test that reads a child's output in the console's encoding reads the same bytes two ways](017-child-stdout-read-in-the-consoles-code-page.md) — applies to: NpcForge.Tests/ConsoleProcess.cs, any test that starts a process and reads its output
- [018 A dev server left on port 5173 is the one the browser tests and checks run against](018-a-dev-server-left-on-the-port-is-the-one-tested.md) — applies to: NpcForge.Web/playwright.config.ts, any check that starts `npm run dev` or `npm start`
- [019 A fixed wall or a scroll box makes the contrast check fail on text it cannot judge](019-text-the-contrast-check-cannot-judge.md) — applies to: NpcForge.Web/src/app.css, NpcForge.Web/src/Sheet.tsx, any `position: fixed`, `sticky` or scrolling `overflow`
- [020 A break-it edit made through shell quoting goes red for the wrong reason](020-a-break-it-edit-through-the-shell-fails-for-the-wrong-reason.md) — applies to: break-it checks, any temporary edit made with `sed` or a heredoc
- [021 A test of the wait passes once the wait has ended, and a held page clock hangs the contrast check](021-a-test-of-the-wait-passes-once-the-wait-has-ended.md) — applies to: NpcForge.Web/tests/wait.spec.ts, NpcForge.Web/src/api.ts, any test that must look at the wait screen
- [022 The flame never reaches an edge, so a test of the edge rule passes with the rule removed](022-the-flame-never-reaches-an-edge-so-the-edge-test-proves-nothing.md) — applies to: NpcForge.Web/src/flameGrid.ts, NpcForge.Web/tests/flame.spec.ts
