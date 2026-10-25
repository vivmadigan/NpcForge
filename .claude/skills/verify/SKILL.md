---
name: verify
description: Drive NpcForge, a .NET console app that starts an MCP server as a child process, and prove a change works without paying for a model call. Use after any edit to NpcForge.Console, NpcForge.Server, the npc-writer SKILL.md or the tests, and before saying a task is done. Replays the recorded run, loads a saved character, probes the flag refusals and runs the free test suite.
---

# Verify NpcForge

NpcForge is a CLI. `dotnet run --project NpcForge.Console` starts `NpcForge.Server` as a child process and talks MCP to it over stdio, with no ports. The server rolls a character brief with dice. A model writes the character, the app saves it, and it prints. The answer is stdout. The trace is stderr, each line prefixed `[app]`, `[server]`, `[skill]`, `[loop]` or `[tool]`. The server has no surface of its own here. It is driven through the console and through `dotnet test`.

## Nothing here costs money

A normal run calls OpenAI, and the owner's user secrets hold real keys. A run is paid unless it uses `--provider replay` or the roll fails first. AGENTS.md says a paid run needs the owner's yes, with the command and why. A paid run is never part of this skill.

`scripts/npcforge-verify.sh run` is the only way this skill starts the console. It enforces three fences:
- `--provider replay` answers from the recorded run in `NpcForge.Tests/Fixtures/replay.jsonl`. The helper refuses any other `--provider`.
- A run without replay must put `--difficulty Nonsense` first. The roll comes before the model's first turn and rejects it. It goes first so a flag under test given last cannot read the next flag as its value. Given the other way round, `--record --difficulty Nonsense` takes `--difficulty` as the file name and fails on a different guard.
- `HTTPS_PROXY` and `HTTP_PROXY` point at `127.0.0.1:9` for the console process only, so a run that ever lost the first two fails at the proxy. The caller's `NO_PROXY` is removed, because a `NO_PROXY` that names the host bypasses the proxy. Checked here with .NET's default `HttpClient`, not through the OpenAI SDK.

The helper also refuses a `--record` path outside the run directory. The error message for `--record` suggests the fixture, and a broken guard plus replay would overwrite it.

## Launch

```bash
dotnet build                                   # first, and again after every edit, SKILL.md included
V=.claude/skills/verify/scripts/npcforge-verify.sh
$V start                                       # prints the evidence directory
$V doctor                                      # ready means no FAIL line
```

There is no server to keep up. Each drive is one `dotnet run` that starts and stops its own server. `--no-build` runs whatever is in `bin/Debug`, so a missing or stale build drives old code. A fresh worktree has no `bin`. The run directory starts empty and the first save creates `save.json` in it. Run `dotnet build` outside the helper, which sets the proxy only for the console.

## Doctor

`$V doctor` is read-only. Run it first, and again whenever anything looks off. It checks:
- `dotnet` is on `PATH`.
- Each of the three projects was built after its newest `.cs` or `.csproj`.
- `bin` holds the same `npc-writer/SKILL.md` as the source. The model is told the `bin` copy.
- The fixture exists and its line 1 is a plain JSON array of flags.
- No `NpcForge.Console` or `NpcForge.Server` process is running. A hit is not necessarily yours. The owner may have one paused in the debugger. Do not kill it.
- `NpcForge.Server/characters.json` is unmodified in `git status`.

## Drive

Every Bash call is a fresh shell, so begin each with `V=.claude/skills/verify/scripts/npcforge-verify.sh; D=$($V dir)`. `features/README.md` indexes what to drive, and each feature file holds the recipe.

| Command | Does |
| --- | --- |
| `$V replay [--split] [--save F\|--temp-save] NAME [-- flags]` | Replays the recorded run with line 1 of the fixture as the answers. Flags after `--` override them. `--temp-save` names no save file, so the app picks a temp one and the helper reads its path from the `[app] saved as` line. |
| `$V load N` | `--load N` against `save.json`, split into `.stdout` and `.stderr`. |
| `$V run [--split] [--save F\|--temp-save] NAME -- flags` | Any console flags, behind the three fences. |
| `$V test [FILTER]` | `dotnet test --no-build`, optionally `--filter "FullyQualifiedName~FILTER"`. |
| `$V fixture-flags` | Prints line 1 of the fixture, one flag per line. |

Each run gets its own save file in the run directory, `save.json` unless `--save` names another. `NPCFORGE_CHARACTERS` is set on the console child only and never exported, so later hand-typed runs in the same shell do not save there. Use `--save` for any run that must not touch `save.json`, such as a refusal or a drift check.

From PowerShell, call Git's bash. The `bash` on `PATH` there is the WSL launcher.

```powershell
$bash = Join-Path (Split-Path (Split-Path (Get-Command git).Source)) "bin\bash.exe"
$out = & $bash .claude/skills/verify/scripts/npcforge-verify.sh doctor 2>&1
$code = $LASTEXITCODE
```

Capture a native command in PowerShell with `$out = <cmd> 2>&1; $code = $LASTEXITCODE`, then filter `$out`. Never pipe the command into `Select-Object -First N`. The pipeline returns while the app is still running and `$LASTEXITCODE` is empty, so every check after it races the save. Never use `> file 2>&1`, which reorders the answer against the trace (lesson 005).

## Evidence

Everything lands in `$TEMP/npcforge-verify/<UTC timestamp>-<pid>/`, which is outside the repo, as `C:/Users/<you>/AppData/Local/Temp/npcforge-verify/...` on Windows. `$V dir` prints the newest one for this checkout. Per run `NAME` the helper writes:
- `NAME.cmd`, the exact command line with its environment.
- `NAME.txt`, stdout and stderr in the order written, or `NAME.stdout` and `NAME.stderr` with `--split`.
- `NAME.exit`, the console's exit code.
- `index.tsv`, one line per run with its exit code and whether the save file exists.
- `save.json`, the exact record of what the app saved.

Proof standards:
- Run the real console as a process, with flags as a user types them and the real server as its child. The only stand-in is the model. Replay skips the vendor SDK and the network, so `ModelClients` openai and anthropic branches are not exercised.
- Capture the action and the state it left: the command, the transcript, the exit code and the save file.
- Check side effects in the save file, never in a console paste. A paste is not byte-exact (lesson 006).
- Pair every negative claim with a positive control. A clean tracked `characters.json` proves nothing if the run died early, so the temp save must exist. A refusal is proved by its exact message, not by a non-zero exit (lesson 004).
- Report a path you could not drive with the command you tried and the precondition that failed. Never report it as verified through another path.

In Git Bash, `<cmd> 2>&1 | tee <file>` keeps both streams in the order written, and the exit code is `${PIPESTATUS[0]}`. The helper does this.

## Break-it loop

To show a new test fail with the code it guards removed:

```bash
sed -i 's/= \["roll_character", "save_character", "load_character"\];/= [];/' NpcForge.Console/McpToolSource.cs
dotnet build && $V test Refuses_an_app_only_tool_the_model_names   # must fail
git checkout -- NpcForge.Console/McpToolSource.cs
git status --short                                                  # must print nothing
dotnet build && $V test Refuses_an_app_only_tool_the_model_names   # must pass again
```

Replace a value (`= null`, `= []`). Do not comment a line out. Commenting the one-statement body of an unbraced `if` makes the next line its body, which gives CS1023 when that line is a declaration and a different guard when it is not. Chain with `&&`, because a failed build leaves the old binary and a bare `--no-build` test then passes against it. Restoring the source does not restore `bin`, so build again before the next test.

## Cleanup

- Processes. Every run ends its own server, and the helper's watchdog (GNU `timeout`, `NPCFORGE_VERIFY_TIMEOUT`, 180 seconds) ends a hung run and its children. Run `$V cleanup` to list any `NpcForge.*` process still up. Never kill by name. Stop only a process you started, by PID. One you did not start belongs to the owner.
- Source. After a break-it edit, `git checkout -- <file>`, then `git status --short` must show only what the task meant to change, and `dotnet build` again.
- Shell. Nothing is exported, so nothing needs unsetting.
- Evidence stays. `cleanup` and the helper never delete the run directory. The OS clears `$TEMP` in time, so copy what a PR needs into the PR.

## Isolate

Two sessions can drive at once. Each run has its own server on stdio and its own save file. Each checkout, worktree or clone keeps its own pointer to its newest run directory. Set `NPCFORGE_VERIFY_DIR` on every call when more than one session is verifying in the same checkout, or they share it. Builds are shared. Never `dotnet build` while another session's run is in flight. A build fails with MSB3021 and MSB3027 while a running or debugger-paused `NpcForge` process holds a dll it needs to replace.

## Gotchas

- An unhandled exception exits `-532462766` in PowerShell and `127` in Git Bash. The helper's own refusals exit `64`, and its watchdog `124`.
- Plain `--load N` builds the real client first, so it needs `OpenAI:ApiKey` in user secrets and fails without it. The helper's `load` passes `--provider replay` and needs no key.
- The replay stops at the first line that differs from the recording. After an edit to the npc-writer `SKILL.md`, a tool description or schema, a `ChatOptions` field or `Tables.cs`, that is the signal to re-record. The re-record is paid. Report the drift. Do not re-record to get green.
- Not covered. A paid run, `--record` for real, and driving the MCP server's stdio directly. Whether a character reads well is *needs my eyes*.
