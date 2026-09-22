---
name: review-environment
description: How to run git, the build and the checks on this Windows machine so review evidence is real
metadata:
  type: reference
---

- `git` is not on the Bash tool's PATH here. Run git and dotnet through the PowerShell tool.
- An incremental `dotnet build` that compiles nothing prints "0 Warning(s)" whatever the code looks like. For the evidence line, run `dotnet build --no-incremental` so the warning count is real, then `dotnet test --no-build`.
- After `dotnet test`, check that no MCP server is left running: `Get-Process -Name NpcForge.Server` and `Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'"` filtered on `*NpcForge.Server*`. None were left at steps 5 to 8; `McpToolSource` is `IAsyncDisposable` and the tests use `await using`.
- Since step 8: hash `NpcForge.Server/characters.json` before and after `dotnet test` (`Get-FileHash`) and list `$env:TEMP\npcforge-test-*`. Same hash and no leftovers proves the tests never touch the user's saves.
- Step tags exist: `step-4` to `step-7`. Diff a finished step against the previous tag, e.g. `git diff step-7 HEAD -- NpcForge.Console`.
- `git diff` excludes a folder with `-- . ':(exclude).claude'` (crew tooling edits are not step code).
- **Proving a lesson-004 test can fail:** `robocopy . <scratchpad>\copy /E /XD bin obj .git .vs .claude docs` (exit code 1 means files copied, not failure), string-replace the filter out of the copy, build, `dotnet test --filter "FullyQualifiedName~<name>"`. If an earlier assert fires first, swap it for a `Console.Error.WriteLine` of the results in the copy and run with `--logger "console;verbosity=detailed"` to see that the call genuinely succeeds. Delete the copy after. Takes about a minute.
- The lead works in parallel: new files (e.g. a `docs/runs/` record) can appear during the review. Re-run `git status --short` just before reporting.

Related: [[review-false-positives]], [[review-recurring-patterns]].
